using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;

/// <summary>Represents a directed unweighted graph structure
/// </summary>
namespace WebCrawler
{
    public class Graph
    {
        private DatabaseConnection db = DatabaseConnection.Instance;
        private List<int> connected_components;
        private int current_list_element;

        public Graph()
        {
            connected_components = new List<int>();
            current_list_element = 0;
        }

        /// <summary>Returns the successors of a given vertex
        /// </summary>
        /// <param name="v">the vertex</param>
        /// <returns>list of all successors of vertex v</returns>
        public List<LinkItem> GetSuccessors(LinkItem v)
        {
            List<LinkItem> item = new List<LinkItem>();
            Query query_select = new Query(String.Format("SELECT url, parent FROM parsed_url where parent = '{0}'", v.Href));
            MySqlDataReader reader;
            if (query_select.ExecuteQuery(db, out reader))
            {
                while (reader.Read())
                {
                    string href = reader.GetString(0);
                    string parent_url = reader.GetString(0);
                    item.Add(new LinkItem(href, 0, parent_url));
                }
                if (reader != null)
                    reader.Close();
            }
            return item;
        }


        public bool IsVisited(LinkItem v)
        {
            Query query_select = new Query(String.Format("SELECT visited FROM parsed_url where url = '{0}'", v.Href));
            MySqlDataReader reader;
            bool isVisited = false;
            if (query_select.ExecuteQuery(db, out reader))
            {
                if (reader.Read())
                    isVisited = reader.GetBoolean(0);
            }
            if (reader != null)
                reader.Close();
            return isVisited;
        }

        public void SetVisited(LinkItem v)
        {
            Query query_update = new Query(String.Format("UPDATE parsed_url SET visited = true WHERE url = '{0}'", v.Href));
            query_update.ExecuteQuery(db);
        }

        public List<int> DFS(List<LinkItem> vertices)
        { 
            Query reset = new Query("UPDATE parsed_url SET visited = false");
            reset.ExecuteQuery(db);
            connected_components.Clear();
            connected_components.Add(0);
            current_list_element = 0;

            foreach (LinkItem v in vertices)
            {
                if (!IsVisited(v))
                {
                    TraverseDFS(v);
                    current_list_element++;
                    connected_components.Add(0);
                }
            }

            for (int i = 0; i < connected_components.Count; i++)
            {
                if (connected_components[i] <= 0)
                    connected_components.RemoveAt(i);
            }

            return connected_components;
        }

        private void TraverseDFS(LinkItem v)
        {
            if (!IsVisited(v))
            {
                SetVisited(v);
                connected_components[current_list_element]++;
                foreach (LinkItem child in GetSuccessors(v))
                    TraverseDFS(child);
            }
        }
    }
}