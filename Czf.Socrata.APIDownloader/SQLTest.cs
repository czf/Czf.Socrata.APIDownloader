using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
namespace Czf.Socrata.APIDownloader;

public class SQLTest
{
    public async Task StreamToDatabase()
    {
        using SqlConnection conn = new SqlConnection("");
        await conn.OpenAsync();
        using SqlBulkCopy bulkCopy = new SqlBulkCopy(conn);
        
    }
}

//https://www.sqlshack.com/import-json-data-into-sql-server/