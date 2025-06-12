using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsedLessCss;
internal class Helps
{
    public static string UnionFiles(params string[] fileContents)
    {
        StringBuilder buf = new();
        foreach (var cssItem in fileContents)
        {
            buf.AppendLine(cssItem);
        }
        return buf.ToString();
    }
    public static void UnionFiles(string path, params string[] fileContents)
    {
        StringBuilder buf = new();
        foreach (var cssItem in fileContents)
        {
            buf.AppendLine(cssItem);
        }
        File.WriteAllText(path, buf.ToString());
    }
}