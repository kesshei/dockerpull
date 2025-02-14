using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DockerPull
{
    public class DockerInfo
    {
        public string RegistryServer { get; set; } = "registry-1.docker.io";
        public string RegistryName { get; set; } = "library";
        public string ImageName { get; set; }
        public string RegistryTag { get; set; } = "latest";
        public string OS { get; set; } = "linux";
        public string Arch { get; set; } = "amd64";
        public string Variant { get; set; }
        public string Proxy { get; set; } = "http://127.0.0.1:1080";
        public static DockerInfo Analysis(string[] args)
        {
            var builder = new ConfigurationBuilder().AddCommandLine(args);
            var configuration = builder.Build();
            Console.WriteLine($"name:{configuration["name"]}"); //name:CLS
            Console.WriteLine($"class:{configuration["class"]}");   //class:Class_A
            return null;
        }
    }
}
