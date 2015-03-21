using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Diagnostics;
using MySql.Data.MySqlClient;

namespace WebCrawler
{
    public class LinkItem
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

    class Program
    {
        static readonly string[] DEFAULT_HOSTNAMES =  { "http://cantarelladanilo.com/" };
        static readonly string[] DEFAULT_KEYWORDS = { "about" };
        static readonly int DEFAULT_MAX_DEPTH = 1;
        static readonly int DEFAULT_COMPUTATION_TIME = 0;

        static Stack<LinkItem> stack = new Stack<LinkItem>();
        static DirectoryInfo output_directory = new DirectoryInfo("./output");
        
        static DatabaseConnection db = DatabaseConnection.Instance;
        static string host = "localhost";
        static string username = "root";
        static string password = "";
        static string db_name = "websites";

        static List<string> hostnames = new List<string>();
        static List<string> keywords = new List<string>();
        static int max_depth = 1;
        static int computation_time = 0;

        enum Parser_Mode
        {
            P_None,
            P_Depth,
            P_Hosts,
            P_Keys,
            P_ComputationTime
        }

        static void Main(string[] args)
        {
            Message.Verbosity = Verbosity_Level.E_Notice;
            Parser_Mode p_mode = Parser_Mode.P_None;
            
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (args.Equals("-debug"))
                        Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Debug;
                    else if (arg.Equals("-warning"))
                        Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Warning;
                    else if (arg.Equals("-error"))
                        Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Error;
                    else if (arg.Equals("-notice"))
                        Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Notice;
                    else if (arg.Equals("-hostnames"))
                        p_mode = Parser_Mode.P_Hosts;
                    else if (arg.Equals("-keywords"))
                        p_mode = Parser_Mode.P_Keys;
                    else if (arg.Equals("-depth"))
                        p_mode = Parser_Mode.P_Depth;
                    else if (arg.Equals("-comptime"))
                        p_mode = Parser_Mode.P_ComputationTime;

                    if (!string.IsNullOrEmpty(arg))
                    {
                        if (p_mode == Parser_Mode.P_Hosts && !arg.Equals("-hostnames"))
                            hostnames.Add(arg);
                        if (p_mode == Parser_Mode.P_Keys && !arg.Equals("-keywords"))
                            keywords.Add(arg);
                        if (p_mode == Parser_Mode.P_Depth && !Int32.TryParse(arg, out max_depth))
                            max_depth = DEFAULT_MAX_DEPTH;
                        if (p_mode == Parser_Mode.P_ComputationTime && !Int32.TryParse(arg, out computation_time))
                            computation_time = DEFAULT_COMPUTATION_TIME;
                    }
                }
            }

            if (hostnames.Count <= 0)
                foreach(string url in DEFAULT_HOSTNAMES)
                    hostnames.Add(url);
            if (keywords.Count <= 0)
                foreach (string key in DEFAULT_KEYWORDS)
                    keywords.Add(key);

            /** creation of directory output **/
            try
            {
                if (!Directory.Exists("./output"))
                    output_directory = Directory.CreateDirectory("./output");
            }
            catch (Exception ex)
            {
                Console.WriteLine("The process failed: {0}", ex.Message);
            }

            /** creation / selection of db **/
            Query query_drop_table = new Query("DROP TABLE IF EXISTS Parsed_Url");
            Query query_create_table = new Query("CREATE TABLE Parsed_Url (url VARCHAR(300) NOT NULL PRIMARY KEY, local_path VARCHAR(300), parent VARCHAR(300) NOT NULL,"
                                                 + " depth INT(11)  NOT NULL, discovered_urls INT(11) NOT NULL)");

            if (db.Connect(host, username, password, db_name))
            {
                query_drop_table.ExecuteQuery(db);
                query_create_table.ExecuteQuery(db);
            }
            else
            {
                if (db.Connect(host, username, password))
                {
                    if (db.CreateDatabase(db_name))
                    {
                        if (db.SelectDatabase(db_name))
                        {
                            query_drop_table.ExecuteQuery(db);
                            query_create_table.ExecuteQuery(db);
                        }
                        else
                        {
                            Message.ShowMessage("Error. Exiting...", Verbosity_Level.E_Error);
                            return;
                        }
                    }
                    else
                    {
                        Message.ShowMessage("Error. Exiting...", Verbosity_Level.E_Error);
                        return;
                    }
                }
                else
                {
                    Message.ShowMessage("Error. Exiting...", Verbosity_Level.E_Error);
                    return;
                }
            }

            Message.ShowMessage("*******************************************", Verbosity_Level.E_Notice);

            string message = "Parsing urls ";
            foreach (string str in hostnames)
                message += string.Format("'{0}' ", str);
            message += "with keywords ";
            foreach (string k in keywords)
                message += string.Format("'{0}' ", k);
            message += string.Format("with depth {0} and computation time {1}", max_depth, computation_time);

            Message.ShowMessage(message, Verbosity_Level.E_Notice);
            Message.ShowMessage("*******************************************", Verbosity_Level.E_Notice);

            foreach (string host_url in hostnames)
                stack.Push(new LinkItem(host_url));
            
            DateTime start_time = DateTime.Now;
            while (stack.Count > 0)
            {
                DateTime current_time = DateTime.Now;
                var diffencence_mins = (current_time - start_time).TotalMinutes;
                if (diffencence_mins > computation_time && computation_time > 0)
                {
                    Message.ShowMessage("Task Completed! Exiting...", Verbosity_Level.E_Notice);
                    break;
                }
                ParsePage(stack.Pop(), keywords, max_depth);
            }
        }

        public static void ParsePage(LinkItem item, List<string> keywords, int max_depth)
        {
            try
            {
                string html_code = String.Empty;
                if (IsURLValid(item.Href) && IsExtensionSupported(item.File_Extension))
                {
                    MySqlDataReader reader = null;
                    Query query_select = new Query(string.Format("SELECT COUNT(*) FROM Parsed_Url WHERE url = '{0}'", item.Href));
                    if (query_select.ExecuteQuery(db, ref reader))
                    {
                        reader.Read();
                        if (reader.GetInt32(0) == 0)
                        {
                            Console.WriteLine("Parsing page {0} with depth {1}", item.Href, item.Depth);
                            if (!reader.IsClosed)
                                reader.Close();
                            Query query_insert = new Query(string.Format("INSERT INTO Parsed_Url VALUES ('{0}', '{1}', '{2}', {3}, {4})", item.Href, item.Local_Path, item.Parent_Url, item.Depth, 0));
                            query_insert.ExecuteQuery(db);
                            html_code = ReadTextFromUrl(item.Href);
                        }
                        else
                        {
                            Console.WriteLine("Page {0} with depth {1} is already know. Skipping...", item.Href, item.Depth);
                            if (!reader.IsClosed)
                                reader.Close();
                            return;
                        }
                    }
                }
             
                if (String.IsNullOrEmpty(html_code))
                    return;

                foreach (string keyword in keywords)
                {
                    if (FindKeyword(html_code, keyword))
                    {
                        Console.WriteLine("Keyword {0} found! Saving...", keyword);

                        string file_path = item.Domain + item.Local_Path;
                        file_path = file_path.Replace(".", "_");
                        file_path = file_path.Replace("/", "_");
                        if (String.IsNullOrEmpty(item.File_Extension))
                            file_path += ".html";
                        file_path = output_directory.FullName + "/" + file_path;

                        using (StreamWriter file = new StreamWriter(file_path))
                        {
                            file.Write(html_code);
                        }
                        break;
                    }
                }

                if (computation_time > 0 || item.Depth < max_depth)
                {
                    List<LinkItem> urls = GetUrlsFromString(html_code);
                    Query query_update = new Query(string.Format("UPDATE Parsed_Url " +
                                                                 "SET discovered_urls = '{0}' " +
                                                                 "WHERE url = '{1}'", urls.Count, item.Href));
                    query_update.ExecuteQuery(db);
                    foreach (LinkItem url in urls)
                    {
                        LinkItem node = new LinkItem(url.Href, item.Depth + 1, item.Href);
                        stack.Push(node);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static string ReadTextFromUrl(string url)
        {
            string data = String.Empty;
            try
            {
                HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
                HttpWebResponse response = (HttpWebResponse) request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream receiveStream = response.GetResponseStream();
                    StreamReader readStream = null;

                    if (response.CharacterSet == null)
                        readStream = new StreamReader(receiveStream);
                    else
                        readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));

                    data = readStream.ReadToEnd();

                    receiveStream.Close();
                    readStream.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return data;
        }
        
        public static bool IsExtensionSupported(string file_ext)
        {
            if (String.IsNullOrEmpty(file_ext))
                return true;

            if (file_ext.EndsWith(".html", StringComparison.Ordinal) || file_ext.EndsWith(".htm", StringComparison.Ordinal) ||
                file_ext.EndsWith(".xhtml", StringComparison.Ordinal) || file_ext.EndsWith(".xml", StringComparison.Ordinal) ||
                file_ext.EndsWith(".php", StringComparison.Ordinal) || file_ext.EndsWith(".jsp", StringComparison.Ordinal) ||
                file_ext.EndsWith(".asp", StringComparison.Ordinal) || file_ext.EndsWith(".aspx", StringComparison.Ordinal) ||
                file_ext.EndsWith(".jsp", StringComparison.Ordinal) || file_ext.EndsWith(".jspx", StringComparison.Ordinal) ||
                file_ext.EndsWith(".do", StringComparison.Ordinal))
                return true;
                
             return false;
        }

        public static bool IsURLValid(string url)
        {
            Uri uriResult;
            return Uri.TryCreate(url, UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public static bool FindKeyword(string html_code, string keyword)
        {
            if (Regex.IsMatch(html_code, keyword, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
            return false;
        }

        public static List<LinkItem> GetUrlsFromString(string html_code)
        {
            List<LinkItem> list = new List<LinkItem>();

            // Find all matches in file.
            MatchCollection m1 = Regex.Matches(html_code, @"(<a.*?>.*?</a>)", RegexOptions.Singleline);

            // Loop over each match.
            foreach (Match m in m1)
            {
                string value = m.Groups[1].Value;
                LinkItem i = new LinkItem();

                // Get href attribute.
                Match m2 = Regex.Match(value, @"href=\""(.*?)\""", RegexOptions.Singleline);
                if (m2.Success)
                    i.Href = m2.Groups[1].Value;

                if (!String.IsNullOrEmpty(i.Href))
                    list.Add(i);
            }
            return list;
        }
    }
}
