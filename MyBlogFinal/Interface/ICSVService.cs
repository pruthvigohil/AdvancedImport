using AdvancedImport.DTO;

namespace AdvancedImport.Interface
{
    public interface ICSVService
    {
        Task<bool> ReadCSV(IFormFile file);
        Type CreateDynamicType(string[] headers);
        void CreateTable(string table, string[] headers);
        void InsertData(string table, List<dynamic> data);
        Task<string> ProcessFile(IFormFile file);
        //public Type CreateDynamicClass(string[] headers);
        //public Task CreateDatabaseTable(Type dynamicClass, string[] headers);
        //public Task InsertRecordsIntoDatabase(List<dynamic> records);

    }
}
