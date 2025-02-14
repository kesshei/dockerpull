using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DockerPull
{
    public class dockerInfo
    {
        public string RegistryServer { get; set; } = "registry-1.docker.io";
        public string RegistryName { get; set; } = "library";
        public string ImageName { get; set; }
        public string RegistryTag { get; set; } = "latest";
        public static dockerInfo Analysis(string args)
        {
            return null;
        }
    }
}
