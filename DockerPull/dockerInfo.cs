using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DockerPull
{
    public static class DirTools
    {
        public static string GetTempFileName(string dir = "")
        {
            var tempDir = string.Empty;
            if (!string.IsNullOrEmpty(dir))
            {
                tempDir = Path.Combine(AppContext.BaseDirectory, dir, Guid.NewGuid().ToString("N"));
            }
            tempDir = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            return tempDir;
        }
    }
    public class DockerInfo
    {
        /// <summary>
        /// 仓库源地址
        /// </summary>
        public string Registry { get; set; } = "registry-1.docker.io";
        /// <summary>
        /// 仓库名
        /// </summary>
        public string Repository { get; set; } = "library";
        /// <summary>
        /// 镜像名
        /// </summary>
        public string ImageName { get; set; }
        /// <summary>
        /// 镜像标记名
        /// </summary>
        public string RegistryTag { get; set; } = "latest";
        /// <summary>
        /// 系统
        /// </summary>
        public string OS { get; set; } = "linux";
        /// <summary>
        /// cpu架构
        /// </summary>
        public string Arch { get; set; } = "amd64";
        public string Variant { get; set; }
        /// <summary>
        /// 代理地址
        /// </summary>
        public string Proxy { get; set; } = "http://127.0.0.1:1080";
        /// <summary>
        /// 临时文件夹
        /// </summary>
        public string tempdir { get; set; } = DirTools.GetTempFileName("temp");
        /// <summary>
        /// 输出目录
        /// </summary>
        public string output { get; set; } = AppContext.BaseDirectory;
        public bool IsVersion2 { get; set; } = true;
        public string GetRegistryUrl()
        {
            if (IsVersion2)
            {
                return $"https://{Registry}/v2/";
            }
            else
            {
                return $"https://{Registry}/";
            }
        }
        public string GetRepository()
        {
            return $"{Repository}/{ImageName}";
        }
        public Dictionary<string, string> Sessions { get; set; } = new Dictionary<string, string>();
        public string tags()
        {
            return string.Join(":", new List<string>() { ImageName, RegistryTag });
        }
        public string tarName()
        {
            return $"{string.Join("_", new string[] { ImageName, RegistryTag })}.tar";
        }
        public string GetVersion()
        {
            var list = new List<string>();
            if (OS != null && OS != "unknown")
            {
                list.Add(OS);
            }
            if (Arch != null && Arch != "unknown")
            {
                list.Add(Arch);
            }
            if (Variant != null && Variant != "unknown")
            {
                list.Add(Variant);
            }
            return string.Join("/", list);
        }
        public HttpClientHandler GetHttpClientHandler()
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            if (!string.IsNullOrEmpty(Proxy))
            {
                handler.Proxy = new WebProxy(Proxy);
            }
            return handler;
        }
        public static DockerInfo Analysis(string[] args)
        {
            if (args.Length > 0)
            {
                var config = Parse(args[0]);
                if (config != null)
                {
                    var builder = new ConfigurationBuilder().AddCommandLine(args);
                    var configuration = builder.Build();
                    if (!string.IsNullOrEmpty(configuration["registry"]))
                    {
                        config.Registry = new Uri(configuration["registry"]).Host;
                    }
                    if (!string.IsNullOrEmpty(configuration["arch"]))
                    {
                        var archs = configuration["arch"].Split("/");
                        if (archs.Length > 0)
                        {
                            config.OS = archs[0];
                        }
                        if (archs.Length > 1)
                        {
                            config.Arch = archs[1];
                        }
                        if (archs.Length > 2)
                        {
                            config.Variant = archs[2];
                        }
                    }
                    if (!string.IsNullOrEmpty(configuration["proxy"]))
                    {
                        config.Proxy = configuration["proxy"];
                    }
                    if (!string.IsNullOrEmpty(configuration["output"]))
                    {
                        config.output = configuration["output"];
                    }
                    return config;
                }
            }
            return null;
        }
        public void Check()
        {
            string[] dockerPullCommands = {
            "docker pull nginx",
            "docker pull nginx:stable-alpine3.20-perl",
            "docker pull registry.baidubce.com/paddlepaddle/paddle:2.6.1-gpu-cuda11.7-cudnn8.4-trt8.4",
            "docker pull bitnami/mysql:latest",
            "docker pull bitnami/mysql:8.4.4-debian-12-r2",
            "docker pull xuxueli/xxl-job-admin:2.4.2",
            "docker pull docker:28.0.0-rc.1-dind-alpine3.21",
            "docker pull audithsoftworks/docker:php-ci",
            "docker pull rancher/rpardini-docker-registry-proxy:0.6.1-amd64",
            "docker pull docker.elastic.co/elasticsearch/elasticsearch:8.0.0-alpha2-arm64",
            "docker pull registry.cn-beijing.aliyuncs.com/205huang/wms-app:v1"
            };
            foreach (var dockerCommand in dockerPullCommands)
            {
                var config = Parse(dockerCommand);
                Console.WriteLine($"命令:{dockerCommand} 解析: domain :{config.Registry} repository:{config.Repository} imageName:{config.ImageName} tag:{config.RegistryTag}");
            }
        }
        public static DockerInfo Parse(string dockerCommand)
        {
            DockerInfo dockerInfo = new DockerInfo();
            string domain = "";
            string repository = "";
            string imageName = "";
            string tag = "";
            var command = dockerCommand.Replace("docker pull ", "");
            var datas = command.Split(":");
            if (datas.Length > 1)
            {
                tag = datas[1];
            }
            var registrys = datas[0].Split("/", StringSplitOptions.RemoveEmptyEntries);
            if (registrys.Length == 1)
            {
                imageName = registrys[0];
            }
            else if (registrys.Length == 2)
            {
                imageName = registrys[1];
                repository = registrys[0];
            }
            else if (registrys.Length == 3)
            {
                imageName = registrys[2];
                repository = registrys[1];
                domain = registrys[0];
                if (domain.Contains(":"))
                {
                    domain = new Uri(domain).Host;
                }
            }


            if (!string.IsNullOrEmpty(imageName))
            {
                dockerInfo.ImageName = imageName;
            }
            else
            {
                return null;
            }
            if (!string.IsNullOrEmpty(domain))
            {
                dockerInfo.Registry = domain;
            }
            if (!string.IsNullOrEmpty(repository))
            {
                dockerInfo.Repository = repository;
            }
            if (!string.IsNullOrEmpty(tag))
            {
                dockerInfo.RegistryTag = tag;
            }
            return dockerInfo;
        }
    }
}
