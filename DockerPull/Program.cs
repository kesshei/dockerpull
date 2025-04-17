using ICSharpCode.SharpZipLib.Tar;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace DockerPull
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //new DockerInfo().Check();
            var config = DockerInfo.Analysis(args);
            await RetryHelper.RetryAsync(async () =>
            {
                await GetRequestHeadAsync(config);
                if (config.Sessions == null)
                {
                    throw new Exception("网络异常，无法连接");
                }
                return "操作成功";
            }, maxRetries: 5);

            if (config.Sessions == null)
            {
                Console.WriteLine($"网络异常，无法连接");
                return;
            }

            var manifestlist = await RetryHelper.RetryAsync(async () =>
            {
                var manifestlist = await GetManifestAsync(config);
                return manifestlist;
            }, maxRetries: 5);
            DigestLayer digestLayer = null;
            if (manifestlist.IsManifest)
            {
                var digest = string.Empty;
                var Manifests = manifestlist.Manifests;
                if (Manifests.schemaVersion < 1)
                {
                    Console.WriteLine($"无法找到镜像信息,请确认输入是否正确!");
                    return;
                }
                if (Manifests.manifests?.Any() == true)
                {
                    var list = GetArchs(Manifests);
                    Console.WriteLine("可选架构列表：");
                    Console.WriteLine(string.Join(Environment.NewLine, list));
                    Console.WriteLine();
                    digest = GetDigest(config, Manifests);
                    if (string.IsNullOrEmpty(digest))
                    {
                        Console.WriteLine($"无法找到此架构:{config.GetVersion()}");
                        Console.WriteLine($"请选择这些可选架构:{string.Join("、", list)}");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine($"无法找到此架构:{config.GetVersion()}");
                    return;
                }

                digestLayer = await RetryHelper.RetryAsync(async () =>
                {
                    var layers = await GetLayerAsync(config, digest);
                    if (layers == null)
                    {
                        throw new Exception("无法找到镜像层信息信息,请重新下载!");
                    }
                    return layers;
                }, maxRetries: 5);
            }
            else
            {
                digestLayer = manifestlist.DigestLayer;
            }

            if (digestLayer == null)
            {
                Console.WriteLine($"无法找到镜像层信息信息,请重新下载!");
                return;
            }
            await Download_layers(config, digestLayer);
            //最后一步，压缩为一个tar文件
            FilesToTar(config);
            Console.WriteLine("清空全部数据，包下载完毕!");

        }
        static async Task GetRequestHeadAsync(DockerInfo dockerInfo)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("Accept", "application/vnd.docker.distribution.manifest.v2+json");
            using (HttpClient client = new HttpClient(dockerInfo.GetHttpClientHandler()))
            {
                try
                {
                    // 发送 HTTP 请求
                    HttpResponseMessage response = await client.GetAsync(dockerInfo.GetRegistryUrl());
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        response.Headers.TryGetValues("WWW-Authenticate", out var WwwAuthenticate);

                        var auth_url = WwwAuthenticate.First().Split('"')[1];
                        var reg_service = WwwAuthenticate.First().Split('"')[3];
                        var url = $"{auth_url}?service={reg_service}&scope=repository:{dockerInfo.GetRepository()}:pull";
                        HttpResponseMessage response2 = await client.GetAsync(url);
                        string json = await response2.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                        dic.Add("Authorization", $"Bearer {result["token"]}");
                        dockerInfo.Sessions = dic;
                    }
                }
                catch (Exception ex)
                {
                    dockerInfo.Sessions = null;
                    Console.WriteLine($"请求出错: {ex.Message}");
                }
            }
        }
        static async Task<ManifestInfos> GetManifestAsync(DockerInfo dockerInfo)
        {
            using (HttpClient client = new HttpClient(dockerInfo.GetHttpClientHandler()))
            {
                try
                {
                    foreach (var item in dockerInfo.Sessions)
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(item.Key, item.Value);
                    }
                    var url = $"{dockerInfo.GetRegistryUrl()}{dockerInfo.GetRepository()}/manifests/{dockerInfo.RegistryTag}";
                    // 发送 HTTP 请求
                    var response = await client.GetAsync(url);
                    string json = await response.Content.ReadAsStringAsync();
                    if (json.IndexOf("manifests") > -1)
                    {
                        var result = await Task.FromResult(JsonSerializer.Deserialize<Manifests>(json));
                        return new ManifestInfos()
                        {
                            IsManifest = true,
                            Manifests = result
                        };
                    }
                    else if (json.IndexOf("layers") > -1 && json.IndexOf("config") > -1)
                    {
                        var result = await Task.FromResult(JsonSerializer.Deserialize<DigestLayer>(json));
                        return new ManifestInfos()
                        {
                            IsManifest = false,
                            DigestLayer = result
                        };
                    }
                    throw new Exception("无法获取到实际的内容");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"请求出错: {ex.Message}");
                }
            }
            return await Task.FromResult<ManifestInfos>(null);
        }

        static List<string> GetArchs(Manifests manifests)
        {
            return manifests.manifests.Where(t => t.platform.IsTrue()).Select(t => t.platform.GetVersion()).ToList();
        }
        static string GetDigest(DockerInfo dockerInfo, Manifests manifests)
        {
            var version = dockerInfo.GetVersion();
            var dd = manifests.manifests.Where(t => t.platform.GetVersion() == version).FirstOrDefault();
            if (dd != null)
            {
                return dd.digest;
            }
            return null;
        }
        static async Task<DigestLayer> GetLayerAsync(DockerInfo dockerInfo, string digest)
        {
            using (HttpClient client = new HttpClient(dockerInfo.GetHttpClientHandler()))
            {
                try
                {
                    foreach (var item in dockerInfo.Sessions)
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(item.Key, item.Value);
                    }
                    var url = $"{dockerInfo.GetRegistryUrl()}{dockerInfo.GetRepository()}/manifests/{digest}";
                    // 发送 HTTP 请求
                    var response = await client.GetAsync(url);
                    string json = await response.Content.ReadAsStringAsync();
                    return await Task.FromResult(JsonSerializer.Deserialize<DigestLayer>(json));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"请求出错: {ex.Message}");
                }
            }
            return await Task.FromResult<DigestLayer>(null);
        }
        static async Task Download_layers(DockerInfo dockerInfo, DigestLayer digestLayer)
        {
            ManifestInfo manifestInfo = new ManifestInfo();
            manifestInfo.RepoTags.Add(dockerInfo.tags());
            //下载镜像描述层
            var ManifestJsonPath = await Download_ManifestJson(dockerInfo, digestLayer);
            manifestInfo.Config = ManifestJsonPath;
            manifestInfo.Layers.AddRange(digestLayer.layers.Select(t => t.GetId()));

            var manifestjsonPath = Path.Combine(dockerInfo.tempdir, "manifest.json");
            File.WriteAllText(manifestjsonPath, JsonSerializer.Serialize(new List<ManifestInfo>() { manifestInfo }));
            Console.WriteLine($"{dockerInfo.RegistryTag}: Pulling from {dockerInfo.GetRepository()}");
            var prgslist = new List<ProgressBar>();
            foreach (var layer in digestLayer.layers)
            {
                var blob_digest = layer.digest;
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                var ProgressBar2 = new ProgressBar(Console.CursorTop - 3, $"{new string(blob_digest.Skip(7).Take(12).ToArray())}");
                ProgressBar2.Change(0);
                await RetryHelper.RetryAsync(async () =>
                {
                    using (HttpClient client = new HttpClient(dockerInfo.GetHttpClientHandler()))
                    {
                        foreach (var item in dockerInfo.Sessions)
                        {
                            client.DefaultRequestHeaders.TryAddWithoutValidation(item.Key, item.Value);
                        }
                        var url = $"{dockerInfo.GetRegistryUrl()}{dockerInfo.GetRepository()}/blobs/{blob_digest}";
                        // 发送 HTTP 请求
                        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        // 获取总字节数
                        var totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
                        ProgressBar2.SetValue("other", $"{0}B / {Tools.GetStringSize(totalBytes)}");
                        var gzipStream = await response.Content.ReadAsStreamAsync();
                        var ids = layer.GetId();
                        var gzipfileName = Path.Combine(dockerInfo.tempdir, $"{ids}.gzip");
                        var fileName = Path.Combine(dockerInfo.tempdir, ids);

                        using (FileStream file = File.Open(gzipfileName, FileMode.CreateNew))
                        {
                            byte[] buffer = new byte[1024 * 4];
                            long bytesRead = 0;
                            int read;

                            while ((read = await gzipStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await file.WriteAsync(buffer, 0, read);
                                bytesRead += read;

                                // 计算下载进度
                                double progress = (double)bytesRead / totalBytes * 100;
                                ProgressBar2.SetValue("other", $"{Tools.GetStringSize(bytesRead)} / {Tools.GetStringSize(totalBytes)}");
                                ProgressBar2.Change((int)progress);
                            }
                        }

                        using (GZipStream decompressionStream = new GZipStream(File.Open(gzipfileName, FileMode.Open), CompressionMode.Decompress))
                        {
                            using (FileStream file = File.Open(fileName, FileMode.CreateNew))
                            {
                                decompressionStream.CopyTo(file);
                            }
                        }
                        File.Delete(gzipfileName);
                    }
                    return "成功";
                }, maxRetries: 5);

                prgslist.Add(ProgressBar2);
            }
            //var repo_tag = "";
            //var tag = "";
            //var fake_layerid = "";
            //var repositoriesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dir, "repositories");
            //File.WriteAllText(repositoriesPath, JsonSerializer.Serialize(new { repo_tag = new { tag = fake_layerid } }));
        }
        static async Task<string> Download_ManifestJson(DockerInfo dockerInfo, DigestLayer digestLayer)
        {
            //下载镜像描述层

            using (HttpClient client = new HttpClient(dockerInfo.GetHttpClientHandler()))
            {
                try
                {
                    foreach (var item in dockerInfo.Sessions)
                    {
                        client.DefaultRequestHeaders.TryAddWithoutValidation(item.Key, item.Value);
                    }
                    var url = $"{dockerInfo.GetRegistryUrl()}{dockerInfo.GetRepository()}/blobs/{digestLayer.config.digest}";
                    // 发送 HTTP 请求
                    var response = await client.GetAsync(url);
                    var json = await response.Content.ReadAsByteArrayAsync();
                    var list = digestLayer.config.digest.Split(":");
                    var fileName = Path.Combine(dockerInfo.tempdir, $"{list[1]}.json");
                    var path = $"{list[1]}.json";
                    CheckFloder(fileName);
                    if (json[0] == 0x1f && json[1] == 0x8b && json[2] == 0x08 && json[3] == 0x00)
                    {
                        using (GZipStream decompressionStream = new GZipStream(new MemoryStream(json), CompressionMode.Decompress))
                        {
                            var newMemoryStream = new MemoryStream();
                            decompressionStream.CopyTo(newMemoryStream);
                            json = newMemoryStream.ToArray();
                        }
                    }
                    File.WriteAllBytes(fileName, json);
                    return await Task.FromResult(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"请求出错: {ex.Message}");
                }
            }
            return await Task.FromResult<string>(null);
        }
        static void CheckFloder(string dir)
        {
            string directoryPath = Path.GetDirectoryName(dir);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
        static void FilesToTar(DockerInfo dockerInfo, bool recurse = true)
        {
            var sourceDirectory = dockerInfo.tempdir;
            var tarFilePath = Path.Combine(dockerInfo.output, dockerInfo.tarName());
            try
            {
                // 创建一个文件流用于写入 tar 文件
                using (FileStream tarFileStream = File.Create(tarFilePath))
                using (TarArchive tarArchive = TarArchive.CreateOutputTarArchive(tarFileStream))
                {
                    // 遍历源文件夹中的所有文件和子文件夹
                    AddDirectoryFilesToTar(tarArchive, sourceDirectory, true);
                }
                Console.WriteLine("打包完成，文件已保存到: " + tarFilePath);
                Directory.Delete(sourceDirectory, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("打包过程中出现错误: " + ex.Message);
            }
        }
        private static void AddDirectoryFilesToTar(TarArchive tarArchive, string sourceDirectory, bool recurse)
        {
            // 获取源文件夹中的所有文件
            string[] filenames = Directory.GetFiles(sourceDirectory);
            foreach (string filename in filenames)
            {
                // 创建一个 TarEntry 对象，表示要添加到 tar 文件中的文件
                TarEntry tarEntry = TarEntry.CreateEntryFromFile(filename);
                // 设置 TarEntry 的名称为相对于源文件夹的路径
                tarEntry.Name = Path.GetRelativePath(sourceDirectory, filename);
                // 将 TarEntry 添加到 tar 存档中
                tarArchive.WriteEntry(tarEntry, false);
            }
            if (recurse)
            {
                // 获取源文件夹中的所有子文件夹
                string[] directories = Directory.GetDirectories(sourceDirectory);
                foreach (string directory in directories)
                {
                    // 递归调用该方法，处理子文件夹中的文件
                    AddDirectoryFilesToTar(tarArchive, directory, recurse);
                }
            }
        }
    }
}

public class Manifests
{
    public Manifest[] manifests { get; set; }
    public string mediaType { get; set; }
    public int schemaVersion { get; set; }
}
public class ManifestInfos
{
    public bool IsManifest { get; set; }
    public Manifests Manifests { get; set; }
    public DigestLayer DigestLayer { get; set; }    
}

public class Manifest
{
    public Annotations annotations { get; set; }
    public string digest { get; set; }
    public string mediaType { get; set; }
    public Platform platform { get; set; }
    public int size { get; set; }
}

public class Annotations
{
    public string comdockerofficialimagesbashbrewarch { get; set; }
    public string orgopencontainersimagebasedigest { get; set; }
    public string orgopencontainersimagebasename { get; set; }
    public DateTime orgopencontainersimagecreated { get; set; }
    public string orgopencontainersimagerevision { get; set; }
    public string orgopencontainersimagesource { get; set; }
    public string orgopencontainersimageurl { get; set; }
    public string orgopencontainersimageversion { get; set; }
    public string vnddockerreferencedigest { get; set; }
    public string vnddockerreferencetype { get; set; }
}

public class Platform
{
    public string architecture { get; set; }
    public string os { get; set; }
    public string variant { get; set; }
    public bool IsTrue()
    {
        if (os != "unknown" && architecture != "unknown")
        {
            return true;
        }
        return false;
    }
    public string GetVersion()
    {
        var list = new List<string>();
        if (os != null && os != "unknown")
        {
            list.Add(os);
        }
        if (architecture != null && architecture != "unknown")
        {
            list.Add(architecture);
        }
        if (variant != null && variant != "unknown")
        {
            list.Add(variant);
        }
        return string.Join("/", list);
    }
}


public class DigestLayer
{
    public int schemaVersion { get; set; }
    public string mediaType { get; set; }
    public Config config { get; set; }
    public Layer[] layers { get; set; }
    public Annotations annotations { get; set; }
}

public class Config
{
    public string mediaType { get; set; }
    public string digest { get; set; }
    public int size { get; set; }
}


public class Layer
{
    public string mediaType { get; set; }
    public string digest { get; set; }
    public int size { get; set; }
    public string GetId()
    {
        return digest.Split(":")[1];
    }
}

public class ManifestInfo
{
    public string Config { get; set; }
    public List<string> RepoTags { get; set; } = new List<string>();
    public List<string> Layers { get; set; } = new List<string>();
}
