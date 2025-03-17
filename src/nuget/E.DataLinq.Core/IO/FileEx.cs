using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Core.IO;

public static class FileEx
{
    //public static string[] ReadBottomLines(string fileName, int numLines)
    //{
    //    string[] lines = new string[numLines];

    //    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    //    {
    //        long endPosition = fs.Length;
    //        int lineCount = 0;

    //        while (lineCount < numLines && endPosition > 0)
    //        {
    //            long currentPosition = Math.Max(0, endPosition - 1024);
    //            fs.Seek(currentPosition, SeekOrigin.Begin);

    //            byte[] buffer = new byte[endPosition - currentPosition];
    //            fs.Read(buffer, 0, buffer.Length);

    //            string bufferContents = Encoding.Default.GetString(buffer);
    //            string[] bufferLines = bufferContents.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

    //            for (int i = bufferLines.Length - 1; i >= 0; i--)
    //            {
    //                string line = bufferLines[i];

    //                if (!string.IsNullOrEmpty(line.Trim()))
    //                {
    //                    lines[lineCount % numLines] = line.Trim();
    //                    lineCount++;
    //                }

    //                if (lineCount == numLines)
    //                {
    //                    break;
    //                }
    //            }

    //            endPosition = currentPosition;
    //        }
    //    }

    //    return lines;
    //}

    async public static Task<string[]> ReadBottomLinesAsync(string fileName, int numLines, string filter = "")
    {
        using (StreamReader sr = new StreamReader(fileName))
        {
            var allLines = (await sr.ReadToEndAsync())
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => !String.IsNullOrEmpty(l) && l.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (allLines.Length <= numLines)
            {
                return allLines.Reverse()
                               .ToArray();
            }

            return allLines.Skip(allLines.Length - numLines)
                           .Reverse()
                           .ToArray();
        }
    }

    async public static Task<string[]> ReadTopLinesAsync(string fileName, int numLines, string filter = "")
    {
        List<string> lines = new List<string>();

        using (StreamReader sr = new StreamReader(fileName))
        {
            for (int i = 0; i < numLines && !sr.EndOfStream; i++)
            {
                string line = (await sr.ReadLineAsync()).Trim();

                if (!String.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }
        }

        return lines.ToArray();
    }
}
