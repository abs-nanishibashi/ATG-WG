using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using GeneralAffairsManagementProject.Utils;

namespace GeneralAffairsManagementProject.Pages
{
    public class NewOrderModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public NewOrderModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 画面から受け取るID
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
        [Range(1, 99, ErrorMessage = "数量は 1～99 の範囲で入力してください。")]
        public int Quantity { get; set; }

        // プルダウン候補
        public List<OrderingMethodDto> OrderingMethods { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<ConsumablesDto> Consumables { get; set; } = new();

        public void OnGet()
        {
            LoadOrderingMethods();

            if (OrderingMethodId.HasValue)
            {
                Categories = LoadCategories(OrderingMethodId.Value);
            }
            if (CategoryId.HasValue)
            {
                Consumables = LoadConsumables(CategoryId.Value);
            }
        }

        // Ajax: 発注方法IDからカテゴリ一覧を返す
        public JsonResult OnGetCategories(int methodId)
        {
            var list = LoadCategories(methodId);
            return new JsonResult(list);
        }

        // Ajax: カテゴリIDから品目一覧を返す
        public JsonResult OnGetConsumables(int categoryId)
        {
            var list = LoadConsumables(categoryId);
            return new JsonResult(list);
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                LoadOrderingMethods();
                if (OrderingMethodId.HasValue) Categories = LoadCategories(OrderingMethodId.Value);
                if (CategoryId.HasValue) Consumables = LoadConsumables(CategoryId.Value);
                return Page();
            }

            string connStr = _configuration.GetConnectionString("GeneralAffairsDb");
            string orderNo = DateTime.Now.ToString("yyyyMMddHHmmss");
            string orderUserName = User.Identity?.Name ?? "system";
            DateTime jstNow = DateTimeUtils.GetJstNow();

            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();
                using var tran = conn.BeginTransaction();

                // 整合性チェック（発注方法・カテゴリ・品目の組み合わせ確認）
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
                        tran.Rollback();
                        ModelState.AddModelError(string.Empty, "選択内容に不整合があります（発注方法・カテゴリ・品目の組み合わせを確認してください）。");
                        LoadOrderingMethods();
                        Categories = LoadCategories(OrderingMethodId.Value);
                        Consumables = LoadConsumables(CategoryId.Value);
                        return Page();
                    }
                }

                // 単価取得（品目IDで取得）
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
                        tran.Rollback();
                        ModelState.AddModelError(string.Empty, "品目が存在しません。");
                        LoadOrderingMethods();
                        Categories = LoadCategories(OrderingMethodId.Value);
                        Consumables = LoadConsumables(CategoryId.Value);
                        return Page();
                    }
                    unitPrice = Convert.ToInt32(result);
                }

                // 親テーブルに登録（発注方法は保持しない）
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

                // 明細テーブルに登録
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

                tran.Commit();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "登録中にエラーが発生しました。" + ex.Message);
                LoadOrderingMethods();
                if (OrderingMethodId.HasValue) Categories = LoadCategories(OrderingMethodId.Value);
                if (CategoryId.HasValue) Consumables = LoadConsumables(CategoryId.Value);
                return Page();
            }

            return RedirectToPage("/Complete");
        }

        public IActionResult OnPostCancel()
        {
            // 新規発注画面の初期表示にリダイレクト
            return RedirectToPage("/NewOrder");
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