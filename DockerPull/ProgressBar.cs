using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DockerPull
{
    public class ProgressBar
    {
        public ProgressBar(int Top, string Content = "")
        {
            this.Top = Top;
            this.dic["name"] = Content;
        }
        public int Top { get; set; }
        public Dictionary<string, string> dic { get; set; } = new Dictionary<string, string>();
        public string ChangeTemplate { get; set; } = "$name: Pulling $value %  $other";
        public string CompleteTemplate { get; set; } = "$name: Pull complete";
        public void Change(int value)
        {
            var Template = ChangeTemplate;
            if (value >= 100)
            {
                Console.SetCursorPosition(0, Top);
                for (int i = 0; ++i < Console.WindowWidth;) { Console.Write(" "); }
                Template = CompleteTemplate;
            }
            Console.SetCursorPosition(0, Top);
            Console.WriteLine(Template.Replace("$name", GetValue("name")).Replace("$value", value.ToString()).Replace("$other", GetValue("other")));

        }
        public string GetValue(string name)
        {
            if (dic.TryGetValue(name, out var value))
            {
                return value;
            }
            return string.Empty;
        }
        public void SetValue(string name, string value)
        {
            dic[name] = value;
        }
        public Task task { get; set; }
    }

}
