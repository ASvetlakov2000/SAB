using System;
using System.IO;
using System.Text;
using System.Net;

namespace SAB.Notifications
{
    public static class ReadableJournal
    {
        static string E(string s) => WebUtility.HtmlEncode(s ?? "");
        const string End = "</main></body></html>";
        public static string Write(string folder, Entry entry)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "SAB-Уведомления-" + DateTime.Now.ToString("yyyy-MM-dd") + ".html");
            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                if (stream.Length == 0)
                {
                    string head = "<!doctype html><html lang='ru'><meta charset='utf-8'><title>SAB — Журнал уведомлений</title>" +
                        "<style>body{font:16px Segoe UI,sans-serif;background:#f7f8fa;color:#1f2937;margin:24px auto;max-width:1000px;padding:0 20px}article{background:white;border:1px solid #d8dee8;border-radius:8px;padding:20px;margin:16px 0}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}input{width:95%;padding:10px;border:1px solid #d8dee8}small{color:#667085}h1{color:#0f6cbd}</style>" +
                        "<body><main><h1>SAB — Журнал уведомлений</h1><p>" + E(path) + "</p><p>Новые записи внизу. Обновите страницу для просмотра новых событий. ID можно выделить и скопировать.</p>";
                    var bytes = Encoding.UTF8.GetBytes(head + End); stream.Write(bytes,0,bytes.Length);
                }
                if (entry != null)
                {
                    var endBytes = Encoding.UTF8.GetBytes(End);
                    stream.Seek(-endBytes.Length,SeekOrigin.End);
                    var actual = new byte[endBytes.Length]; stream.Read(actual,0,actual.Length);
                    if (Encoding.UTF8.GetString(actual) != End) throw new IOException("Повреждён HTML-журнал");
                    stream.Seek(-endBytes.Length,SeekOrigin.End);
                    string html = "<article><small>" + E(entry.Time) + " • " + E(entry.Severity) + "</small><h2>" + E(entry.Transaction ?? "Диалог Revit") +
                        "</h2><p><b>Модель:</b> " + E(entry.Model) + "</p><pre>" + E(entry.Text) + "</pre><p><b>Действие:</b> " + E(entry.Action) +
                        "</p><label>ID элементов через запятую<br><input readonly aria-label='ID элементов' value='" + E(entry.Ids) + "'></label><p><small>Код: " + E(entry.Failure) + "</small></p></article>" + End;
                    var bytes = Encoding.UTF8.GetBytes(html); stream.Write(bytes,0,bytes.Length); stream.SetLength(stream.Position);
                }
            }
            return path;
        }
    }
}
