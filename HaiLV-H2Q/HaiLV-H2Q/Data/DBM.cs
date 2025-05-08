using MySql.Data.MySqlClient;
using System.Data;

namespace HaiLV_H2Q.Data
{
    public class DBM
    {
        private readonly string _connectionString;

        public DBM(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public DataTable ExecuteStoredProcedure(string procedureName, Dictionary<string, object> parameters)
        {
            using var connection = new MySqlConnection(_connectionString);
            using var command = new MySqlCommand(procedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }

            connection.Open();
            using var adapter = new MySqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            return dataTable;
        }

        public void ExecuteNonQuery(string procedureName, Dictionary<string, object> parameters)
        {
            using var connection = new MySqlConnection(_connectionString);
            using var command = new MySqlCommand(procedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            foreach (var param in parameters)
            {
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }

            connection.Open();
            command.ExecuteNonQuery();
        }
    }
}