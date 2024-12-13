using CsvHelper.Configuration;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdvancedImport.DTO;
using AdvancedImport.Interface;
using System.Globalization;
using AdvancedImport.Classes;
using System.Dynamic;
using Microsoft.VisualBasic.FileIO;
using System.IO;

namespace AdvancedImport.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly MyBlogDbContext dbContext;
        private readonly ICSVService _csvService;

        public HomeController(MyBlogDbContext dbContext, ICSVService cSVService)
        {
            this.dbContext = dbContext;
            _csvService = cSVService;
        }

        [HttpPost("Post-Sales-Records")]
        public async Task<IActionResult> ImportFromCSV(IFormFile formFile)
        {
            try
            {
                if (formFile == null || formFile.Length == 0)
                    return BadRequest("Invalid file");

                var result = await _csvService.ReadCSV(formFile);

                return result ? Ok("Data inserted successfully") : NoContent();
            }
            catch (Exception ex)
            {
                return Ok("Exception occurred: " + ex.ToString());
            }
        }

        [HttpGet("Get-Sales-Records")]
        public async Task<IActionResult> GetSalesRecords()
        {
            try
            {
                var result = await this.dbContext.SalesRecords.ToListAsync();
                if (result != null && result.Count > 0)
                {
                    return Ok(result.Take(50));
                }
                return BadRequest("No Data found");
            }
            catch (Exception ex)
            {
                return Ok("Exception occurred: " + ex.ToString());
            }
        }

        [HttpPost("UploadCSV-DynamicInsert")]
        public async Task<IActionResult> UploadCSV(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Please Upload Proper File");
            }
            else
            {
                string isSuccess = await _csvService.ProcessFile(file);
                return Ok(isSuccess);
            }
        }

    }
}
