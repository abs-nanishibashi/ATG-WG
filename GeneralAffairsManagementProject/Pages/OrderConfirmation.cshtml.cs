using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace GeneralAffairsManagementProject.Pages
{
    public class OrderConfirmationModel : PageModel
    {
        private readonly ILogger<OrderConfirmationModel> _logger;
        private readonly IDbConnection _db;

        public OrderConfirmationModel(ILogger<OrderConfirmationModel> logger, IDbConnection db)
        {
            _logger = logger;
            _db = db;
        }

        public void OnGet()
        {
            // 画面初期表示用（HTML側の描画のみ）
        }

        // GET: /Index?handler=Tables
        public async Task<JsonResult> OnGetTablesAsync()
        {

            try
            {

                const string sql = @"
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_SCHEMA, TABLE_NAME";

                await using var conn = (SqlConnection)_db;
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();

                var list = new List<object>();
                while (await rdr.ReadAsync())
                    list.Add(new { schema = rdr.GetString(0), name = rdr.GetString(1) });

                return new JsonResult(list);

            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message, detail = ex.ToString() });
            }

        }

        // GET: /Index?handler=Columns
        public async Task<JsonResult> OnGetColumnsAsync()
        {
            try
            {
                const string sql = @"
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION";

                await using var conn = (SqlConnection)_db;
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                using var rdr = await cmd.ExecuteReaderAsync();

                var list = new List<Dictionary<string, object?>>();
                while (await rdr.ReadAsync())
                {
                    list.Add(new()
                    {
                        ["schema"] = rdr["TABLE_SCHEMA"],
                        ["table"] = rdr["TABLE_NAME"],
                        ["column"] = rdr["COLUMN_NAME"],
                        ["type"] = rdr["DATA_TYPE"],
                        ["maxLen"] = rdr["CHARACTER_MAXIMUM_LENGTH"] is DBNull ? null : rdr["CHARACTER_MAXIMUM_LENGTH"],
                        ["nullable"] = rdr["IS_NULLABLE"],
                        ["default"] = rdr["COLUMN_DEFAULT"] is DBNull ? null : rdr["COLUMN_DEFAULT"]
                    });
                }
                return new JsonResult(list);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message, detail = ex.ToString() });
            }
        }

        // GET: /Index?handler=Constraints
        public async Task<JsonResult> OnGetConstraintsAsync()
        {
            try
            {
                const string pkUniqueSql = @"
SELECT 
    TC.CONSTRAINT_TYPE, TC.CONSTRAINT_NAME, TC.TABLE_SCHEMA, TC.TABLE_NAME, KU.COLUMN_NAME, KU.ORDINAL_POSITION
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KU ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE IN ('PRIMARY KEY','UNIQUE')
ORDER BY TC.TABLE_SCHEMA, TC.TABLE_NAME, TC.CONSTRAINT_NAME, KU.ORDINAL_POSITION";

                const string fkSql = @"
SELECT  
    fk.name AS FK_NAME,
    SCHEMA_NAME(pt.schema_id) AS PARENT_SCHEMA,
    pt.name AS PARENT_TABLE,
    pc.name AS PARENT_COLUMN,
    SCHEMA_NAME(rt.schema_id) AS REF_SCHEMA,
    rt.name AS REF_TABLE,
    rc.name AS REF_COLUMN
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
ORDER BY PARENT_SCHEMA, PARENT_TABLE, FK_NAME";

                await using var conn = (SqlConnection)_db;
                await conn.OpenAsync();

                var result = new Dictionary<string, object>();

                var pkUnq = new List<Dictionary<string, object>>();
                using (var cmd1 = new SqlCommand(pkUniqueSql, conn))
                using (var rdr1 = await cmd1.ExecuteReaderAsync())
                {
                    while (await rdr1.ReadAsync())
                    {
                        pkUnq.Add(new()
                        {
                            ["type"] = rdr1["CONSTRAINT_TYPE"],  // PRIMARY KEY / UNIQUE
                            ["name"] = rdr1["CONSTRAINT_NAME"],
                            ["schema"] = rdr1["TABLE_SCHEMA"],
                            ["table"] = rdr1["TABLE_NAME"],
                            ["column"] = rdr1["COLUMN_NAME"],
                            ["position"] = rdr1["ORDINAL_POSITION"]
                        });
                    }
                }

                var fks = new List<Dictionary<string, object>>();
                using (var cmd2 = new SqlCommand(fkSql, conn))
                using (var rdr2 = await cmd2.ExecuteReaderAsync())
                {
                    while (await rdr2.ReadAsync())
                    {
                        fks.Add(new()
                        {
                            ["name"] = rdr2["FK_NAME"],
                            ["parentSchema"] = rdr2["PARENT_SCHEMA"],
                            ["parentTable"] = rdr2["PARENT_TABLE"],
                            ["parentColumn"] = rdr2["PARENT_COLUMN"],
                            ["refSchema"] = rdr2["REF_SCHEMA"],
                            ["refTable"] = rdr2["REF_TABLE"],
                            ["refColumn"] = rdr2["REF_COLUMN"]
                        });
                    }
                }

                result["primaryOrUnique"] = pkUnq;
                result["foreignKeys"] = fks;
                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                return new JsonResult(new
                {
                    error = ex.Message,
                    detail = ex.ToString()
                });
            }
        }
    }
}
