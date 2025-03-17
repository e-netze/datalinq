#define simulate__

using E.DataLinq.Core.Engines.Abstraction;
using E.DataLinq.Core.Engines.Reader;
using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Models;
using E.DataLinq.Core.Services.Abstraction;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace E.DataLinq.Core.Engines;

public class DatabaseEngine : IDataLinqSelectEngine, IDataLinqExecuteNonQueryEngine
{
    private readonly IEnumerable<IDbFactoryProviderService> _factories;
    private readonly IEnumerable<IEngineFieldParserService> _fieldParser;
    private readonly IDataLinqEnvironmentService _environment;

    public DatabaseEngine(IEnumerable<IDbFactoryProviderService> factories,
                          IEnumerable<IEngineFieldParserService> fieldParser,
                          IDataLinqEnvironmentService environment)
    {
        _factories = factories;
        _fieldParser = fieldParser;
        _environment = environment;
    }

    #region IDataLinqSelectEngine

    public int EndpointType => (int)DefaultEndPointTypes.Database;

    async public Task<bool> TestConnection(DataLinqEndPoint endPoint)
    {
        string connectionString = endPoint.GetConnectionString(_environment);
        var factoryProvider = _factories?.Where(f => f.SupportsConnection(connectionString)).FirstOrDefault();

        if (factoryProvider != null)
        {
            var factory = factoryProvider.GetFactory();

            using (var connection = factory.CreateConnection())
            {
                connection.ConnectionString = factoryProvider.RawConnectionString(connectionString);

                await connection.OpenAsync();
            }
        }

        return true;
    }

    async public Task<(object[] records, bool isOrdered)> SelectAsync(DataLinqEndPoint endPoint, DataLinqEndPointQuery query, NameValueCollection arguments)
    {
        bool isOrdered = false;
        List<object> result = new List<object>();

        string connectionString = endPoint.GetConnectionString(_environment);
        var factoryProvider = _factories?.Where(f => f.SupportsConnection(connectionString)).FirstOrDefault();

        if (factoryProvider != null)
        {
            var factory = factoryProvider.GetFactory();

            using (var connection = factory.CreateConnection())
            {
                connection.ConnectionString = factoryProvider.RawConnectionString(connectionString);

                var command = factory.CreateCommand();
                command.Connection = connection;

                string sql = query.Statement.ParseStatement(arguments, StatementType.Sql);

                foreach (var parameterName in arguments.AllKeys)
                {
                    if (String.IsNullOrWhiteSpace(parameterName))
                    {
                        continue;
                    }

                    if (parameterName.ToLower() == "_orderby" && (sql.Contains("@" + parameterName) || sql.Contains(":" + parameterName)))
                    {
                        isOrdered = true;
                        // Achtung SQL Injektion!!!
                        string term = GetSqlValue(arguments[parameterName])?.ToString().ParsePro(',');
                        List<string> orderItems = new List<string>();
                        foreach (string orderField in term.Split(','))
                        {
                            if (orderField.StartsWith("-"))
                            {
                                orderItems.Add(orderField.Substring(1) + " DESC");
                            }
                            else
                            {
                                orderItems.Add(orderField);
                            }
                        }

                        sql = sql.Replace("@" + parameterName, String.Join(",", orderItems));
                    }
                    else if (sql.Contains("@" + parameterName) || sql.Contains(":" + parameterName))
                    {
                        var parameter = factory.CreateParameter();
                        parameter.ParameterName = parameterName;
                        parameter.Value = GetSqlValue(arguments[parameterName]);

                        command.Parameters.Add(parameter);
                    }

                    if (sql.Contains("{{" + parameterName + "}}"))
                    {
                        // Achtung SQL Injektion!!!
                        string term = GetSqlValue(arguments[parameterName])?.ToString().ParsePro(',');
                        sql = sql.Replace("{{" + parameterName + "}}", term);
                    }
                }

                command.CommandText = sql;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                using (var recordReader = new DbRecordReader(reader))
                {
                    while (await reader.ReadAsync())
                    {
                        ExpandoObject expando = new ExpandoObject();
                        IDictionary<string, object> expandoDict = (IDictionary<string, object>)expando;
                        for (int i = 0, to = reader.FieldCount; i < to; i++)
                        {
                            var val = _fieldParser.ParseAny(recordReader, reader.GetName(i)) ?? reader.GetValue(i);

                            // damit könnten auch SqlType.Geometries abgefragt werden?!
                            //if(!(val is string) && !val.GetType().IsValueType)
                            //{
                            //    val = val.ToString();
                            //}

                            expandoDict.Add(reader.GetName(i), val);
                        }
                        result.Add(expando);
                    }
                }
            }
        }

        return (records: result.ToArray(), isOrdered: isOrdered);
    }

    #endregion

    #region IDataLinqExecuteNonQueryEngine

    async public Task<bool> ExecuteNonQueryAsync(DataLinqEndPoint endPoint,
                                                 DataLinqEndPointQuery query,
                                                 NameValueCollection form)
    {
        string connectionString = endPoint.GetConnectionString(_environment);
        var factoryProvider = _factories?.Where(f => f.SupportsConnection(connectionString)).FirstOrDefault();

        if (factoryProvider != null)
        {
            var factory = factoryProvider.GetFactory();

            using (var transaction = new DBTransaction(factory.CreateConnection(), factoryProvider.RawConnectionString(connectionString)))
            {
                var command = factory.CreateCommand();
                command.Connection = transaction.Connnection;
#if !simulate
                command.Transaction = transaction.Transaction;
#endif
                string sql = query.Statement;

                foreach (string parameterName in form.Keys)
                {
                    if (String.IsNullOrWhiteSpace(parameterName))
                    {
                        continue;
                    }

                    if (sql.Contains("@" + parameterName) || sql.Contains(":" + parameterName))
                    {
                        object val;
                        switch (form[parameterName]?.ToString())
                        {
                            case "null":
                            case "NULL":
                                val = DBNull.Value;
                                break;
                            default:
                                val = form[parameterName]?.ToString();
                                break;
                        }

                        var parameter = factory.CreateParameter();
                        parameter.ParameterName = parameterName;
                        parameter.Value = val;

                        command.Parameters.Add(parameter);
                    }

                    if (sql.Contains("{{" + parameterName + "}}"))
                    {
                        // Check for SQL Injektion!!!
                        string term = form[parameterName].ParsePro(',');
                        sql = sql.Replace("{{" + parameterName + "}}", term);
                    }
                }

                command.CommandText = sql;
#if simulate
                Console.WriteLine($"EXECUTE NONE QUERY: { sql }");
#else
                int result = await command.ExecuteNonQueryAsync();
                transaction.Commit();
#endif
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Helper

    private object GetSqlValue(string val)
    {
        if (val != null)
        {
            switch (val.ToLower())
            {
                case "string.empty":
                    return String.Empty;
                case "dbnull.value":
                    return DBNull.Value;
            }
        }

        return val;
    }

    #endregion

    #region HelperClasses

    private class DBTransaction : IDisposable
    {
        public DBTransaction(DbConnection connection, string connectionString)
        {
            this.Connnection = connection;
            this.Connnection.ConnectionString = connectionString;
            this.Connnection.Open();

            this.Transaction = this.Connnection.BeginTransaction();
            this.Commited = false;
        }

        public DbConnection Connnection { get; set; }

        public DbTransaction Transaction { get; set; }

        private bool Commited { get; set; }

        public void Commit()
        {
            this.Transaction.Commit();
            this.Commited = true;
        }

        #region IDisposable

        public void Dispose()
        {
            if (!Commited)
            {
                this.Transaction.Rollback();
            }

            this.Connnection.Close();
            this.Connnection.Dispose();
        }

        #endregion
    }

    #endregion
}
