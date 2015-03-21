using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using MySql.Data.MySqlClient;

namespace WebCrawler
{
    sealed class DatabaseConnection
    {
        private static readonly DatabaseConnection instance = new DatabaseConnection();
        private MySqlConnection m_Connection;

        private DatabaseConnection()
        {
        }

        public static DatabaseConnection Instance
        {
            get
            {
                return instance;
            }
        }

        ~DatabaseConnection()
        {
            if (m_Connection.State == ConnectionState.Open)
                m_Connection.Close();
        }

        public MySqlConnection GetSqlConnection()
        {
            return m_Connection;
        }

        public bool Connect(string server, string username, string password, string database_name = null)
        {
            bool success = true;
            try
            {
                string m_ConnectionString = "server=" + server + ";";
                m_ConnectionString += "userid = " + username + ";";
                m_ConnectionString += "password=" + password + ";";
                m_ConnectionString += "database=";
                if (database_name != null)
                    m_ConnectionString += database_name;
                m_ConnectionString += ";";

                m_Connection = new MySqlConnection(m_ConnectionString);
                m_Connection.Open();
            }
            catch (MySqlException ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
                return false;
            }
            if (success)
            {
                Message.ShowMessage("Connection Established", Verbosity_Level.E_Notice);
                Message.ShowMessage("MySQL version : {0}", Verbosity_Level.E_Notice, m_Connection.ServerVersion);
            }
            return success;
        }

        public bool CreateDatabase(string db_name)
        {
            Query query = new Query("CREATE DATABASE IF NOT EXISTS " + db_name);
            if (query.ExecuteQuery(this))
            {
                Message.ShowMessage("Database {0} Created", Verbosity_Level.E_Notice, db_name);
                return true;
            }
            return false;
        }

        public bool SelectDatabase(string db_name)
        {
            bool success = true;
            try
            {
                m_Connection.ChangeDatabase(db_name);
            }
            catch (MySqlException ex)
            {
                Message.ShowMessage("Error : {0}", Verbosity_Level.E_Error, ex.Message);
                success = false;
            }
            if (success)
                Message.ShowMessage("Database {0} Selected", Verbosity_Level.E_Notice, m_Connection.Database);
            return success;
        }
    }
}

