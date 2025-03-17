using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Text;

namespace E.DataLinq.Code.Controllers;

public class DataLinqCodeBaseController : Controller
{
    protected DataLinqCodeBaseController() { }

    protected IActionResult JsonObject(object obj, bool pretty = false)
    {
        MemoryStream ms = new MemoryStream();

        var jw = new Newtonsoft.Json.JsonTextWriter(new StreamWriter(ms));
        jw.Formatting = pretty ?
            Newtonsoft.Json.Formatting.Indented :
            Newtonsoft.Json.Formatting.None;
        var serializer = new Newtonsoft.Json.JsonSerializer();
        serializer.Serialize(jw, obj);
        jw.Flush();
        ms.Position = 0;

        string json = System.Text.Encoding.UTF8.GetString(ms.GetBuffer());
        json = json.Trim('\0');

        return JsonResultStream(json);
    }

    protected IActionResult JsonResultStream(string json)
    {
        json = json ?? String.Empty;

        Response.Headers.Append("Pragma", "no-cache");
        Response.Headers.Append("Cache-Control", "no-cache, no-store, max-age=0, must-revalidate");
        Response.Headers.Append("Access-Control-Allow-Headers", "*");
        Response.Headers.Append("Access-Control-Allow-Origin", (string)Request.Headers["Origin"] != null ? (string)Request.Headers["Origin"] : "*");
        Response.Headers.Append("Access-Control-Allow-Credentials", "true");

        return BinaryResultStream(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8");
    }

    protected IActionResult BinaryResultStream(byte[] data, string contentType, string fileName = "")
    {
        if (!String.IsNullOrWhiteSpace(fileName))
        {
            Response.Headers.Append("Content-Disposition", "attachment; filename=\"" + fileName + "\"");
        }

        return File(data, contentType);
    }

    protected IActionResult PlainResultStream(string text, string contantType)
    {
        text = text ?? String.Empty;

        return BinaryResultStream(Encoding.UTF8.GetBytes(text), contantType);
    }
}
