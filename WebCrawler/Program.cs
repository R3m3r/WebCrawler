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
    class Program
    {
        static readonly int DEFAULT_MAX_DEPTH = 1;
        static readonly int DEFAULT_COMPUTATION_TIME = 0;
        static readonly string[] domain_to_search = { ".org", ".com", ".edu", ".it", ".uk", ".dk", ".de", ".se" };

        static readonly string DEFAULT_HOSTNAME = "localhost";
        static readonly string DEFAULT_USERNAME = "root";
        static readonly string DEFAULT_PASSWORD = "";
        static readonly string DEFAULT_DB_NAME = "websites";
        
        static DatabaseConnection db = DatabaseConnection.Instance;
        static string host = DEFAULT_HOSTNAME;
        static string username = DEFAULT_USERNAME;
        static string password = DEFAULT_PASSWORD;
        static string db_name = DEFAULT_DB_NAME;

        static SqlStack stack = new SqlStack();
        static DirectoryInfo output_directory = new DirectoryInfo("./output");

        static List<string> hostnames = new List<string>();
        static List<string> keywords = new List<string>();
        static int max_depth = DEFAULT_MAX_DEPTH;
        static int computation_time = DEFAULT_COMPUTATION_TIME;

        static Process currentProc = Process.GetCurrentProcess();
        
        enum Parser_Mode
        {
            P_None,
            P_Depth,
            P_Hosts,
            P_Keys,
            P_ComputationTime,
            P_DBName,
            P_DBHost,
            P_DBUsername,
            P_DBPassword
        }

        static void Main(string[] args)
        {
            Message.Verbosity = Verbosity_Level.E_Notice | Verbosity_Level.E_Error;
            Parser_Mode p_mode = Parser_Mode.P_None;
            List<LinkItem> starting_nodes = new List<LinkItem>();
            bool resume = false;

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
            Query query_create_table = new Query("CREATE TABLE Parsed_Url (url VARCHAR(300) NOT NULL PRIMARY KEY, local_path VARCHAR(300), parent VARCHAR(300) NOT NULL,"
                                                 + " depth INT(11)  NOT NULL, discovered_urls INT(11) NOT NULL, visited BOOLEAN)");
            Query query_stack_db = new Query("CREATE TABLE Stack_Url (url VARCHAR(300) NOT NULL, parent_url VARCHAR(300), depth int, PRIMARY KEY (url))");

            if (db.Connect(host, username, password, db_name))
            {
                query_create_table.ExecuteQuery(db);
                query_stack_db.ExecuteQuery(db);
            }
            else
            {
                if (db.Connect(host, username, password))
                {
                    if (db.CreateDatabase(db_name))
                    {
                        if (db.SelectDatabase(db_name))
                        {
                            query_create_table.ExecuteQuery(db);
                            query_stack_db.ExecuteQuery(db);
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

            /** parsing argument **/
            if (args != null)
            {
                foreach (string arg in args)
                {
                    switch (arg.ToLower())
                    {
                        case "-debug":
                            Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Debug;
                            break;
                        case "-warning":
                            Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Warning;
                            break;
                        case "-error":
                            Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Error;
                            break;
                        case "-notice":
                            Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Notice;
                            break;
                        case "-hostnames":
                            p_mode = Parser_Mode.P_Hosts;
                            break;
                        case "-keywords":
                            p_mode = Parser_Mode.P_Keys;
                            break;
                        case "-comptime":
                            p_mode = Parser_Mode.P_ComputationTime;
                            break;
                        case "-depth":
                            p_mode = Parser_Mode.P_Depth;
                            break;
                        case "-resume":
                            resume = true;
                            break;
                        case "-dbhost":
                            p_mode = Parser_Mode.P_DBHost;
                            break;
                        case "-dbusername":
                            p_mode = Parser_Mode.P_DBUsername;
                            break;
                        case "-dbname":
                            p_mode = Parser_Mode.P_DBName;
                            break;
                        case "-dbpassword":
                            p_mode = Parser_Mode.P_DBPassword;
                            break;
                    }

                    if (!string.IsNullOrEmpty(arg))
                    {
                        if (p_mode == Parser_Mode.P_Hosts && !arg.Equals("-hostnames"))
                            hostnames.Add(arg);
                        if (p_mode == Parser_Mode.P_Keys && !arg.Equals("-keywords"))
                            keywords.Add(arg);
                        if (p_mode == Parser_Mode.P_DBHost && !arg.Equals("-dbhost"))
                            host = arg;
                        if (p_mode == Parser_Mode.P_DBName && !arg.Equals("-dbname"))
                            host = arg;
                        if (p_mode == Parser_Mode.P_DBPassword && !arg.Equals("-dbpassword"))
                            host = arg;
                        if (p_mode == Parser_Mode.P_DBUsername && !arg.Equals("-dbusername"))
                            host = arg;
                        if (p_mode == Parser_Mode.P_Depth && !Int32.TryParse(arg, out max_depth))
                            max_depth = DEFAULT_MAX_DEPTH;
                        if (p_mode == Parser_Mode.P_ComputationTime && !Int32.TryParse(arg, out computation_time))
                            computation_time = DEFAULT_COMPUTATION_TIME;
                    }
                }
            }

            if (!resume)
            {
                Query query_truncate_url = new Query("TRUNCATE TABLE Parsed_Url");
                Query query_truncate_stack = new Query("TRUNCATE TABLE Stack_Url");
                query_truncate_url.ExecuteQuery(db);
                query_truncate_stack.ExecuteQuery(db);
            }

            if (keywords.Count == 0)
            {
                Message.ShowMessage("Keywords must be set!", Verbosity_Level.E_Error);
                return;
            }

            if (hostnames.Count == 0 && !resume)
            {
                Message.ShowMessage("Hostnames must be set!", Verbosity_Level.E_Error);
                return;
            }

            /** push the input url **/
            foreach (string host_url in hostnames)
            {
                LinkItem link = new LinkItem(host_url);
                starting_nodes.Add(link);
                stack.Push(link);
            }
            Message.ShowMessage("***************************************************", Verbosity_Level.E_Notice);

            string message = "Parsing urls ";
            foreach (string str in hostnames)
                message += string.Format("'{0}' ", str);
            message += "with keywords ";
            foreach (string k in keywords)
                message += string.Format("'{0}' ", k);
            message += string.Format("with depth {0} and computation time {1}", max_depth, computation_time);

            Message.ShowMessage(message, Verbosity_Level.E_Notice);
            Message.ShowMessage("***************************************************", Verbosity_Level.E_Notice);

            DateTime start_time = DateTime.Now;
            while (stack.Count() > 0)
            {
                DateTime current_time = DateTime.Now;
                var diffencence_mins = (current_time - start_time).TotalMinutes;
                if (computation_time > 0 && diffencence_mins > computation_time)
                    break;
                ParsePage(stack.Pop(), keywords, max_depth);
                ShowMemoryInfo();
            }
            ShowMemoryInfo(true);

            /** Connected graph components and diameters **/
            Graph graph = new Graph();
            List<int> connected_components = graph.DFS(starting_nodes);
            int diameter = connected_components[0];
            foreach (int component in connected_components)
                if (diameter < component)
                    diameter = component;
            Message.ShowMessage("Connected components: {0}", Verbosity_Level.E_Notice, connected_components.Count);
            Message.ShowMessage("Max diameter of graph: {0}", Verbosity_Level.E_Notice, diameter);

            /** discovered and parsed urls **/
            Query disc_url_query = new Query(String.Format("SELECT SUM(discovered_urls) AS TotalDiscoveredUrls FROM parsed_url"));
            Query parsed_url_query =  new Query(String.Format("SELECT COUNT(*) FROM parsed_url"));

            disc_url_query.ExecuteQueryReader(db);
            int discovered_urls = disc_url_query.GetInt32(0, true);

            parsed_url_query.ExecuteQueryReader(db);
            int parsed_url = parsed_url_query.GetInt32(0, true);
            
            Message.ShowMessage("Discovered urls: {0}", Verbosity_Level.E_Notice, discovered_urls);
            Message.ShowMessage("Parsed urls: {0}", Verbosity_Level.E_Notice, parsed_url);

            /** stats for domains **/
            foreach (string domain in domain_to_search)
            {
                Query query_domain_stats = new Query(String.Format("SELECT COUNT(*) FROM parsed_url WHERE url LIKE '%{0}%'", domain));
                query_domain_stats.ExecuteQueryReader(db);
                int domain_count = query_domain_stats.GetInt32(0, true);
                Message.ShowMessage("Domains {0} discovered: {1}", Verbosity_Level.E_Notice, domain, domain_count);
            }

            /** stats of depths **/
            for (int i = 1; i < 4; i++)
            {
                Query query_depth_stats = new Query(String.Format("SELECT COUNT(*) FROM parsed_url WHERE depth = {0}", i));
                query_depth_stats.ExecuteQueryReader(db);
                int depth_count = query_depth_stats.GetInt32(0, true);
                Message.ShowMessage("Domains of depth {0} discovered: {1}", Verbosity_Level.E_Notice, i, depth_count);
            }

            Message.ShowMessage("***************************************************", Verbosity_Level.E_Notice);
            Message.ShowMessage("Task Completed! Exiting...", Verbosity_Level.E_Notice);
        }

        public static void ParsePage(LinkItem item, List<string> keywords, int max_depth)
        {
            try
            {
                string html_code = String.Empty;
                if (IsURLValid(item.Href) && IsExtensionSupported(item.File_Extension))
                {
                    MySqlDataReader reader;
                    Query query_select = new Query(string.Format("SELECT COUNT(*) FROM Parsed_Url WHERE url = '{0}'", item.Href));
                    if (query_select.ExecuteQuery(db, out reader))
                    {
                        reader.Read();
                        if (reader.GetInt32(0) == 0)
                        {
                            Console.WriteLine("Parsing page {0} with depth {1}", item.Href, item.Depth);
                            if (!reader.IsClosed)
                                reader.Close();
                            Query query_insert = new Query(string.Format("INSERT INTO parsed_url (url, local_path, parent, depth) VALUES ('{0}', '{1}', '{2}', {3});", 
                                                                item.Href, item.Local_Path, item.Parent_Url, item.Depth));
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
                    Query query_update_urls = new Query(string.Format("UPDATE Parsed_Url " +
                                                                 "SET discovered_urls = '{0}' " +
                                                                 "WHERE url = '{1}'", urls.Count, item.Href));
                    query_update_urls.ExecuteQuery(db);
                    
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

        public static void ShowMemoryInfo(bool showPeak = false)
        {
            // Refresh the current process property values.
            currentProc.Refresh();

            // Display current process statistics.
            Message.ShowMessage("  Physical memory usage: {0}",
                Verbosity_Level.E_Notice, currentProc.WorkingSet64 / 1024);
            Message.ShowMessage("  PagedSystemMemorySize64: {0}",
                Verbosity_Level.E_Notice, currentProc.PagedSystemMemorySize64 / 1024);
            Message.ShowMessage("  PagedMemorySize64: {0}",
               Verbosity_Level.E_Notice, currentProc.PagedMemorySize64 / 1024);

            Message.ShowMessage("-------------------------------------", Verbosity_Level.E_Notice);

            if (showPeak)
            {
                // Display peak memory statistics for the process.
                Message.ShowMessage("Peak physical memory usage of the process: {0}",
                    Verbosity_Level.E_Notice, currentProc.PeakWorkingSet64 / 1024);
                Message.ShowMessage("Peak paged memory usage of the process: {0}",
                    Verbosity_Level.E_Notice, currentProc.PeakPagedMemorySize64 / 1024);
                Message.ShowMessage("Peak virtual memory usage of the process: {0}",
                    Verbosity_Level.E_Notice, currentProc.PeakVirtualMemorySize64 / 1024);
                Message.ShowMessage("***************************************************", Verbosity_Level.E_Notice);
            }
        }
    }
}
