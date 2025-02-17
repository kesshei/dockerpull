﻿using Microsoft.Extensions.Configuration;
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
        public string Registry { get; set; } = "registry-1.docker.io";
        public string Repository { get; set; } = "library";
        public string ImageName { get; set; }
        public string RegistryTag { get; set; } = "latest";
        public string OS { get; set; } = "linux";
        public string Arch { get; set; } = "amd64";
        public string Variant { get; set; }
        public string Proxy { get; set; } = "http://127.0.0.1:1080";
        public string tempdir { get; set; } = DirTools.GetTempFileName("temp");
        public bool canUse { get; set; }
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
            var builder = new ConfigurationBuilder().AddCommandLine(args);
            var configuration = builder.Build();
            Console.WriteLine($"name:{configuration["name"]}"); //name:CLS
            Console.WriteLine($"class:{configuration["class"]}");   //class:Class_A


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
                string domain = "";
                string repository = "";
                string imageName = "";
                string tag = "latest"; // 默认标记为 latest
                var command = dockerCommand.Replace("docker pull ", "").Replace("https://", "").Replace("http://", "");
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
                }
                Console.WriteLine($"命令:{command} 解析: domain :{domain} repository:{repository} imageName:{imageName} tag:{tag}");
            }
        }
    }
}
