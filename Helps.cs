using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsedLessCss;
internal class Helps
{
    public static void UnionFiles(string path, params byte[][] bytes)
    {
        byte[] contentBytes = UnionFiles(bytes);
        File.WriteAllBytes(path, contentBytes);
    }

    public static byte[] UnionFiles(params byte[][] bytes)
    {
        MemoryStream ms = new();
        ms.Write([239, 187, 191]);
        foreach (byte[] item in bytes)
        {
            // 去掉UTF8签名字节(3个:239,187,191)
            if (item[0] == 239 && item[1] == 187 && item[2] == 191)
                ms.Write(item.AsSpan()[3..]);
            else
                ms.Write(item);
        }
        return ms.GetBuffer();
    }
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
        string content = UnionFiles(fileContents);
        File.WriteAllText(path, content);
    }
}