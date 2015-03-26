using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;

namespace WebCrawler
{
    class Query
    {
        private string query_sql;

        public Query(string query)
        {
            query_sql = query;
            query_sql = query_sql.Replace(", )", ")");
            query_sql = query_sql.Replace(",)", ")");
        }

        public bool ExecuteQuery(DatabaseConnection database)
        {
            try
            {
                MySqlCommand cmd = new MySqlCommand(query_sql, database.GetSqlConnection());
                Message.ShowMessage(query_sql, Verbosity_Level.E_Debug);
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {
                if (ex.Message.StartsWith("Duplicate entry"))
                    Message.ShowMessage(ex.Message, Verbosity_Level.E_Warning);
                else
                    Console.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

        public bool ExecuteQuery(DatabaseConnection database, out MySqlDataReader reader)
        {
            reader = null;
            try
            {
                MySqlCommand cmd = new MySqlCommand(query_sql, database.GetSqlConnection());
                Message.ShowMessage(query_sql, Verbosity_Level.E_Debug);
                reader = cmd.ExecuteReader();
            }
            catch (MySqlException ex)
            {
                Message.ShowMessage("Error: {0}", Verbosity_Level.E_Error, ex.Message);
                return false;
            }
            return true;
        }
    }
}
