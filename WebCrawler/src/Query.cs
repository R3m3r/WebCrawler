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
        private MySqlDataReader reader_2;

        public Query(string query)
        {
            query_sql = query;
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

        public bool ExecuteQueryReader(DatabaseConnection database)
        {
            reader_2 = null;
            try
            {
                MySqlCommand cmd = new MySqlCommand(query_sql, database.GetSqlConnection());
                Message.ShowMessage(query_sql, Verbosity_Level.E_Debug);
                reader_2 = cmd.ExecuteReader();
            }
            catch (MySqlException ex)
            {
                Message.ShowMessage("Error: {0}", Verbosity_Level.E_Error, ex.Message);
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

        public int GetInt32(int column_to_read, bool close_reader)
        {
            int return_value = -1;
            try
            {
                reader_2.Read();
                return_value = reader_2.GetInt32(column_to_read);
            }
            catch (MySqlException ex)
            {
                Message.ShowMessage("Error: {0}", Verbosity_Level.E_Error, ex.Message);
            }
            finally
            {
                if (close_reader && !reader_2.IsClosed)
                    reader_2.Close();
            }
            return return_value;
        }
    }
}
