using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Diagnostics;
using MySql.Data.MySqlClient;

namespace WebCrawler
{
    class LinkItem
    {
        public string Protocol = "";
        public string Domain = "";
        public string Local_Path = "";
        public string File_Extension = "";
        public string Parent_Url = "";
        public int Depth = 0;
        public string Href
        {
            get
            {
                return Protocol + "://" + Domain + Local_Path;
            }
            set
            {
                GetUrlInfo(value);
            }
        }

        public LinkItem(string href = "", int depth = 0, string parent = "")
        {
            GetUrlInfo(href);
            Depth = depth;
            Parent_Url = parent;
        }

        public void GetUrlInfo(string url)
        {
            try
            {
                if (!url.StartsWith("http") && !url.StartsWith("https"))
                    url = "http://" + url;
                Uri uri = new Uri(url);
                Domain = uri.Host;
                Local_Path = uri.AbsolutePath;
                File_Extension = Path.GetExtension(uri.AbsolutePath);
                Protocol = uri.Scheme;
            }
            catch (UriFormatException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    class SqlStack
    {
        private DatabaseConnection db = DatabaseConnection.Instance;

        public void Push(LinkItem item)
        {
            Query query_insert = new Query(string.Format("INSERT INTO Stack_Url (url, parent_url, depth) VALUES ('{0}', '{1}', '{2}')", item.Href, item.Parent_Url, item.Depth));
            query_insert.ExecuteQuery(db);
        }

        public LinkItem Pop()
        {
            LinkItem item = null;
            Query query_select = new Query("SELECT * FROM stack_url where depth = (SELECT min(depth) FROM stack_url)");
            MySqlDataReader reader;
            if (query_select.ExecuteQuery(db, out reader))
            {
                reader.Read();
                string href = reader.GetString(0);
                string parent_url = reader.GetString(1);
                int depth = reader.GetInt32(2);
                item = new LinkItem(href, depth, parent_url);
                if (reader != null)
                    reader.Close();
                Query query_delete = new Query(string.Format("DELETE FROM stack_url WHERE url = '{0}'", href));
                query_delete.ExecuteQuery(db);
            }
            return item;
        }

        public int Count()
        {
            int ret = 0;
            Query query_select = new Query("SELECT COUNT(*) FROM Stack_Url");
            MySqlDataReader reader;
            if (query_select.ExecuteQuery(db, out reader))
            {
                reader.Read();
                ret = reader.GetInt32(0);
            }
            if (reader != null)
                reader.Close();
            return ret;
        }
    }
}
