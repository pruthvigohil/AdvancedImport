using System.Data;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using AdvancedImport.Classes;
using AdvancedImport.DTO;
using AdvancedImport.Interface;

namespace AdvancedImport.Services
{
    public class CSVService : ICSVService
    {
        private readonly MyBlogDbContext dbContext;
        private readonly IConfiguration _configuration;

        public CSVService(MyBlogDbContext dbContext, IConfiguration configuration)
        {
            this.dbContext = dbContext;
            _configuration = configuration;
        }

        public async Task<bool> ReadCSV(IFormFile file)
        {
            try
            {
                using (var streamReader = new StreamReader(file.OpenReadStream()))
                using (var csvReader = new CsvReader(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ",",
                    Encoding = Encoding.UTF8,
                    HasHeaderRecord = true
                }))
                {
                    var records = csvReader.GetRecords<SalesRecordsDTO>().ToList();
                    if (records.Count > 0)
                    {
                        List<SalesRecords> salesRecords = records.Select(x => new SalesRecords
                        {
                            Region = x.Region,
                            Country = x.Country,
                            ItemType = x.ItemType,
                            SalesChannel = x.SalesChannel,
                            OrderPriority = x.OrderPriority,
                            OrderDate = DateTime.Parse(x.OrderDate, CultureInfo.InvariantCulture),
                            OrderID = Convert.ToInt32(x.OrderID),
                            ShipDate = DateTime.Parse(x.ShipDate, CultureInfo.InvariantCulture),
                            UnitsSold = Convert.ToInt32(x.UnitsSold),
                            UnitPrice = Convert.ToDecimal(x.UnitPrice),
                            UnitCost = Convert.ToDecimal(x.UnitCost),
                            TotalRevenue = Convert.ToDecimal(x.TotalRevenue),
                            TotalCost = Convert.ToDecimal(x.TotalCost),
                            TotalProfit = Convert.ToDecimal(x.TotalProfit)
                        }).ToList();
                        await this.dbContext.SalesRecords.AddRangeAsync(salesRecords);
                        await this.dbContext.SaveChangesAsync();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public Type CreateDynamicType(string[] headers)
        {
            var assemblyName = new AssemblyName("DynamicAssembly");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
            var typeBuilder = moduleBuilder.DefineType("DynamicClass", TypeAttributes.Public);

            foreach (var header in headers)
            {
                typeBuilder.DefineField(header.Replace(" ", "_"), typeof(string), FieldAttributes.Public);
            }

            return typeBuilder.CreateType();
        }

        public async Task<string> ProcessFile(IFormFile file)
        {
            try
            {
                using (var parser = new TextFieldParser(file.OpenReadStream()))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");

                    string[] line;
                    while (!parser.EndOfData)
                    {
                        try
                        {
                            line = parser.ReadFields();
                        }
                        catch (MalformedLineException ex)
                        {
                            return "File Parsing Failed";
                        }
                    }
                }
                //Begin to Read CSV
                using (var reader = new StreamReader(file.OpenReadStream()))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    await csv.ReadAsync();
                    csv.ReadHeader();
                    var headers = csv.Context.Reader.HeaderRecord;
                    var records = new List<dynamic>();
                    while (await csv.ReadAsync())
                    {
                        var record = new ExpandoObject() as IDictionary<string, object>;
                        foreach (var header in headers)
                        {
                            record[header] = csv.GetField(header);
                        }
                        records.Add(record);
                    }

                    var dynamicType = CreateDynamicType(headers);

                    var tableName = Path.GetFileNameWithoutExtension(file.FileName).Replace(" ", "_");
                    CreateTable(tableName, headers);

                    InsertData(tableName, records);

                    return "File Import Success";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public void CreateTable(string table, string[] headers)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    DataSet ds = new DataSet();
                    var columns = string.Join(", ", headers.Select(header => $"[{header}] NVARCHAR(MAX)"));
                    var createTableQuery = $"IF  NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{table}]') AND type in (N'U')) BEGIN CREATE TABLE [{table}] ({columns}) END";
                    //var createTableQuery = $"CREATE TABLE [{table}] ({columns})";

                    command.CommandText = createTableQuery;
                    command.ExecuteNonQuery();
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public void InsertData(string table, List<dynamic> data)
        {

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                var batchSize = 1000;
                var batches = data.Select((record, index) => new { Record = record, Index = index })
                                     .GroupBy(x => x.Index / batchSize)
                                     .Select(group => group.Select(x => x.Record).ToList())
                                     .ToList();

                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var batch in batches)
                    {
                        var columns = string.Join(", ", ((IDictionary<string, object>)batch.First()).Keys.Select(key => $"[{key}]"));
                        var values = string.Join(", ", batch.Select(record =>
                        {
                            var valuesList = ((IDictionary<string, object>)record).Values.Select(value =>
                            {
                                var strValue = value?.ToString()?.Replace("'", "''") ?? "NULL";
                                return $"'{strValue}'";
                            });
                            return "(" + string.Join(", ", valuesList) + ")";
                        }));

                        var commandText = $"INSERT INTO [{table}] ({columns}) VALUES {values}";

                        using (var command = new SqlCommand(commandText, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        //public DateTime ConvertToDateTime(string date)
        //{
        //    DateTime dateTime;
        //    if (DateTime.TryParseExact(date, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
        //    {
        //        return dateTime;
        //    }
        //    else
        //    {
        //        return dateTime;
        //    }

        //}

        // Method to create a dynamic class based on CSV headers
        //public Type CreateDynamicClass(string[] headers)
        //{
        //    var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DynamicAssembly"), AssemblyBuilderAccess.Run);
        //    var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
        //    var typeBuilder = moduleBuilder.DefineType("DynamicClass", TypeAttributes.Public);

        //    // Define 'Id' property as primary key
        //    var idProperty = typeBuilder.DefineProperty("Id", PropertyAttributes.None, typeof(int), null);
        //    var idField = typeBuilder.DefineField("_id", typeof(int), FieldAttributes.Private);

        //    var getMethodBuilder = typeBuilder.DefineMethod("get_Id", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
        //    var getIl = getMethodBuilder.GetILGenerator();
        //    getIl.Emit(OpCodes.Ldarg_0);
        //    getIl.Emit(OpCodes.Ldfld, idField);
        //    getIl.Emit(OpCodes.Ret);

        //    var setMethodBuilder = typeBuilder.DefineMethod("set_Id", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, new Type[] { typeof(int) });
        //    var setIl = setMethodBuilder.GetILGenerator();
        //    setIl.Emit(OpCodes.Ldarg_0);
        //    setIl.Emit(OpCodes.Ldarg_1);
        //    setIl.Emit(OpCodes.Stfld, idField);
        //    setIl.Emit(OpCodes.Ret);

        //    idProperty.SetGetMethod(getMethodBuilder);
        //    idProperty.SetSetMethod(setMethodBuilder);

        //    foreach (var header in headers)
        //    {
        //        typeBuilder.DefineProperty(header.Replace(" ", "_"), PropertyAttributes.None, typeof(string), null);
        //    }

        //    return typeBuilder.CreateType();
        //}

        //// Method to create a table in the database dynamically
        //public async Task CreateDatabaseTable(Type dynamicClass, string[] headers)
        //{
        //    string sqlQuery = $"CREATE TABLE {dynamicClass.Name} (Id INT PRIMARY KEY IDENTITY";

        //    foreach (var header in headers)
        //    {
        //        // Add additional columns based on CSV headers
        //        sqlQuery += $", [{header.Replace(" ", "_")}] NVARCHAR(MAX)";
        //    }

        //    sqlQuery += ")";

        //    // Execute the SQL query to create the table
        //    await dbContext.Database.ExecuteSqlRawAsync(sqlQuery);
        //}

        //// Method to insert records into the dynamically created table
        //public async Task InsertRecordsIntoDatabase(List<dynamic> records)
        //{
        //    // Insert records into the dynamically created table using Entity Framework Core
        //    //await dbContext.DynamicRecords.AddRangeAsync(records);
        //    await dbContext.SaveChangesAsync();
        //}
    }
}
