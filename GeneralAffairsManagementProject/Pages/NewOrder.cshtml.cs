using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using GeneralAffairsManagementProject.Utils;

namespace GeneralAffairsManagementProject.Pages
{
    public class NewOrderModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<NewOrderModel> _logger;

        public NewOrderModel(IConfiguration configuration, ILogger<NewOrderModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // IDs received from the page
        [BindProperty]
        [Required(ErrorMessage = "発注方法を選択してください。")]
        public int? OrderingMethodId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "カテゴリを選択してください。")]
        public int? CategoryId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "品目を選択してください。")]
        public int? ConsumablesId { get; set; }

        [BindProperty]
        [Range(1, 99, ErrorMessage = "数量は1～99の範囲で入力してください。")]
        public int Quantity { get; set; }

        // Dropdown candidates
        public List<OrderingMethodDto> OrderingMethods { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<ConsumablesDto> Consumables { get; set; } = new();

        public void OnGet()
        {
            // Info for request correlation (separate from AI operation_Id; for app-side tracing)
            var correlationId = HttpContext.TraceIdentifier;
            var userName = User.Identity?.Name ?? "anonymous";

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["Handler"] = "OnGet",
                ["CorrelationId"] = correlationId,
                ["UserName"] = userName
            }))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("New order page: initial load started");

                try
                {
                    LoadOrderingMethods();
                    _logger.LogInformation("Ordering method candidates loaded count={Count}", OrderingMethods.Count);

                    if (OrderingMethodId.HasValue)
                    {
                        Categories = LoadCategories(OrderingMethodId.Value);
                        _logger.LogInformation("Category candidates loaded methodId={MethodId} count={Count}",
                            OrderingMethodId.Value, Categories.Count);
                    }

                    if (CategoryId.HasValue)
                    {
                        Consumables = LoadConsumables(CategoryId.Value);
                        _logger.LogInformation("Item candidates loaded categoryId={CategoryId} count={Count}",
                            CategoryId.Value, Consumables.Count);
                    }

                    _logger.LogInformation("New order page: initial load completed elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred during new order page initial load elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    throw; // If the page fails to load, rethrow so it can be detected as an exception
                }
            }
        }

        // Ajax: return category list by ordering method id
        public JsonResult OnGetCategories(int methodId)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userName = User.Identity?.Name ?? "anonymous";

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["Handler"] = "OnGetCategories",
                ["CorrelationId"] = correlationId,
                ["UserName"] = userName,
                ["MethodId"] = methodId
            }))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Get categories (Ajax) started");

                try
                {
                    var list = LoadCategories(methodId);
                    _logger.LogInformation("Get categories (Ajax) succeeded count={Count} elapsedMs={ElapsedMs}",
                        list.Count, sw.ElapsedMilliseconds);
                    return new JsonResult(list);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Get categories (Ajax) failed elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    // Do not return too much detail to the client
                    Response.StatusCode = 500;
                    return new JsonResult(new { error = "Failed to retrieve categories." });
                }
            }
        }

        // Ajax: return item list by category id
        public JsonResult OnGetConsumables(int categoryId)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userName = User.Identity?.Name ?? "anonymous";

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["Handler"] = "OnGetConsumables",
                ["CorrelationId"] = correlationId,
                ["UserName"] = userName,
                ["CategoryId"] = categoryId
            }))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Get items (Ajax) started");

                try
                {
                    var list = LoadConsumables(categoryId);
                    _logger.LogInformation("Get items (Ajax) succeeded count={Count} elapsedMs={ElapsedMs}",
                        list.Count, sw.ElapsedMilliseconds);
                    return new JsonResult(list);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Get items (Ajax) failed elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    Response.StatusCode = 500;
                    return new JsonResult(new { error = "Failed to retrieve items." });
                }
            }
        }

        public IActionResult OnPost()
        {
            var correlationId = HttpContext.TraceIdentifier;
            var orderUserName = User.Identity?.Name ?? "system";
            var orderNo = DateTime.Now.ToString("yyyyMMddHHmmss");
            var jstNow = DateTimeUtils.GetJstNow();

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["Handler"] = "OnPost",
                ["CorrelationId"] = correlationId,
                ["OrderNo"] = orderNo,
                ["UserName"] = orderUserName,
                ["OrderingMethodId"] = OrderingMethodId,
                ["CategoryId"] = CategoryId,
                ["ConsumablesId"] = ConsumablesId,
                ["Quantity"] = Quantity
            }))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("Create new order started");

                if (!ModelState.IsValid)
                {
                    // Input issues are warnings, not errors
                    _logger.LogWarning("Input validation failed. Re-displaying the page.");
                    ReloadForPage();
                    return Page();
                }

                string connStr = _configuration.GetConnectionString("GeneralAffairsDb");

                try
                {
                    using var conn = new SqlConnection(connStr);
                    conn.Open();
                    _logger.LogInformation("Database connection succeeded");

                    using var tran = conn.BeginTransaction();
                    _logger.LogInformation("Transaction started");

                    // 1) Consistency check
                    _logger.LogInformation("Consistency check started");
                    using (var cmd = new SqlCommand(@"
SELECT COUNT(1)
FROM TM_CONSUMABLES s
JOIN TM_CONSUMABLES_CATEGORY c ON c.ID = s.CATEGORY_ID
WHERE s.ID = @ConsumablesId
  AND s.DELETE_FLAG = 0
  AND c.ID = @CategoryId
  AND c.METHOD_ID = @MethodId
  AND c.DELETE_FLAG = 0;
", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@ConsumablesId", ConsumablesId!.Value);
                        cmd.Parameters.AddWithValue("@CategoryId", CategoryId!.Value);
                        cmd.Parameters.AddWithValue("@MethodId", OrderingMethodId!.Value);

                        var ok = (int)cmd.ExecuteScalar() == 1;
                        if (!ok)
                        {
                            _logger.LogWarning("Consistency check failed (invalid combination). Rolling back.");
                            tran.Rollback();
                            ModelState.AddModelError(string.Empty, "The selected values are inconsistent. Please verify the ordering method, category, and item combination.");
                            ReloadForPage();
                            return Page();
                        }
                    }
                    _logger.LogInformation("Consistency check passed");

                    // 2) Get unit price
                    _logger.LogInformation("Unit price lookup started");
                    int unitPrice;
                    using (var cmd = new SqlCommand(@"
SELECT UNIT_PRICE
FROM TM_CONSUMABLES
WHERE ID = @ConsumablesId
  AND DELETE_FLAG = 0;
", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@ConsumablesId", ConsumablesId!.Value);
                        object? result = cmd.ExecuteScalar();
                        if (result == null)
                        {
                            _logger.LogWarning("Unit price lookup failed (item does not exist). Rolling back.");
                            tran.Rollback();
                            ModelState.AddModelError(string.Empty, "The selected item does not exist.");
                            ReloadForPage();
                            return Page();
                        }
                        unitPrice = Convert.ToInt32(result);
                    }
                    _logger.LogInformation("Unit price lookup succeeded unitPrice={UnitPrice}", unitPrice);

                    // 3) Insert header
                    _logger.LogInformation("Order header insert started");
                    int newOrderId;
                    using (var cmd = new SqlCommand(@"
INSERT INTO TD_ORDER
    (ORDER_NO,
     ORDER_DATE,
     CREATE_DATETIME,
     UPDATE_DATETIME,
     ORDER_USER_NAME,
     CONTACT_ORDER_NAME,
     ORDER_NOTE,
     ORDER_STATUS_ID,
     DELETE_FLAG)
OUTPUT INSERTED.ID
VALUES
    (@OrderNo,
     @OrderDate,
     @CreateDateTime,
     @UpdateDateTime,
     @OrderUserName,
     @ContactOrderName,
     NULL,
     1,
     0);
", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@OrderNo", orderNo);
                        cmd.Parameters.AddWithValue("@OrderDate", jstNow);
                        cmd.Parameters.AddWithValue("@CreateDateTime", jstNow);
                        cmd.Parameters.AddWithValue("@UpdateDateTime", jstNow);
                        cmd.Parameters.AddWithValue("@OrderUserName", orderUserName);
                        cmd.Parameters.AddWithValue("@ContactOrderName", orderUserName);

                        newOrderId = (int)cmd.ExecuteScalar();
                    }
                    _logger.LogInformation("Order header insert succeeded newOrderId={NewOrderId}", newOrderId);

                    // 4) Insert detail
                    _logger.LogInformation("Order detail insert started");
                    using (var cmd = new SqlCommand(@"
INSERT INTO TD_ORDER_DETAILS
    (ORDER_ID,
     SEQ_NO,
     CONSUMABLES_ID,
     ORDER_QUANTITY,
     ORDER_UNIT_NAME,
     ORDERING_TIME_UNIT_PRICE,
     SCHEDULED_DELIVERY_DATE,
     DELIVERYED_FLAG,
     ORDER_DETAILS_NOTE,
     DELETE_FLAG,
     CREATE_DATETIME,
     UPDATE_DATETIME)
VALUES
    (@OrderId,
     1,
     @ConsumablesId,
     @OrderQuantity,
     @OrderUnitName,
     @UnitPrice,
     NULL,
     0,
     NULL,
     0,
     @CreateDateTime,
     @UpdateDateTime);
", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", newOrderId);
                        cmd.Parameters.AddWithValue("@ConsumablesId", ConsumablesId!.Value);
                        cmd.Parameters.AddWithValue("@OrderQuantity", Quantity);
                        cmd.Parameters.AddWithValue("@OrderUnitName", "NoData");
                        cmd.Parameters.AddWithValue("@UnitPrice", unitPrice);
                        cmd.Parameters.AddWithValue("@CreateDateTime", jstNow);
                        cmd.Parameters.AddWithValue("@UpdateDateTime", jstNow);

                        cmd.ExecuteNonQuery();
                    }
                    _logger.LogInformation("Order detail insert succeeded");

                    // 5) Commit
                    tran.Commit();
                    _logger.LogInformation("Transaction committed elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);

                    return RedirectToPage("/Complete");
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "SQL exception (DB connection/query/transaction) elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    ModelState.AddModelError(string.Empty, "A database error occurred.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception (create new order) elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    ModelState.AddModelError(string.Empty, "An error occurred during registration.");
                }

                // Re-display only when an exception occurs
                ReloadForPage();
                return Page();
            }
        }

        public IActionResult OnPostCancel()
        {
            _logger.LogInformation("New order canceled");
            return RedirectToPage("/NewOrder");
        }

        private void ReloadForPage()
        {
            LoadOrderingMethods();
            if (OrderingMethodId.HasValue) Categories = LoadCategories(OrderingMethodId.Value);
            if (CategoryId.HasValue) Consumables = LoadConsumables(CategoryId.Value);
        }

        private void LoadOrderingMethods()
        {
            string connStr = _configuration.GetConnectionString("GeneralAffairsDb");
            using var conn = new SqlConnection(connStr);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT ID, NAME
FROM TM_ORDERING_METHOD
WHERE DELETE_FLAG = 0
ORDER BY ID;
", conn);

            using var reader = cmd.ExecuteReader();
            OrderingMethods.Clear();
            while (reader.Read())
            {
                OrderingMethods.Add(new OrderingMethodDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }

        private List<CategoryDto> LoadCategories(int methodId)
        {
            string connStr = _configuration.GetConnectionString("GeneralAffairsDb");
            using var conn = new SqlConnection(connStr);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT ID, CATEGORY_NAME
FROM TM_CONSUMABLES_CATEGORY
WHERE METHOD_ID = @MethodId
  AND DELETE_FLAG = 0
ORDER BY ID;
", conn);

            cmd.Parameters.AddWithValue("@MethodId", methodId);

            using var reader = cmd.ExecuteReader();
            var list = new List<CategoryDto>();
            while (reader.Read())
            {
                list.Add(new CategoryDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
            return list;
        }

        private List<ConsumablesDto> LoadConsumables(int categoryId)
        {
            string connStr = _configuration.GetConnectionString("GeneralAffairsDb");
            using var conn = new SqlConnection(connStr);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT ID, ITEM_NAME
FROM TM_CONSUMABLES
WHERE CATEGORY_ID = @CategoryId
  AND DELETE_FLAG = 0
ORDER BY ID;
", conn);

            cmd.Parameters.AddWithValue("@CategoryId", categoryId);

            using var reader = cmd.ExecuteReader();
            var list = new List<ConsumablesDto>();
            while (reader.Read())
            {
                list.Add(new ConsumablesDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
            return list;
        }

        public class OrderingMethodDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class CategoryDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class ConsumablesDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}