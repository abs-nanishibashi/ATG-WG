using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;

namespace GeneralAffairsManagementProject.Pages
{
    public class NewOrderModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public NewOrderModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 画面から受け取る値たち
        [BindProperty]
        public string OrderType { get; set; }   // 発注方法（オンライン/電話/FAX）

        [BindProperty]
        public string Category { get; set; }    // カテゴリ（食品/工具）

        [BindProperty]
        public string ItemName { get; set; }    // 品目名（米/パン/ドライバー/ハンマー）

        [BindProperty]
        [Range(1, 99, ErrorMessage = "数量は 1～99 の範囲で入力してください。")]
        public int Quantity { get; set; }       // 数量

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            // クライアント側の required などに対応する
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // 接続文字列
            string connStr = _configuration.GetConnectionString("GeneralAffairsDb");

            // 発注番号（重複しないように秒まで使う）
            string orderNo = DateTime.Now.ToString("yyyyMMddHHmmss");

            // 発注者名（ログインユーザー名 or system）
            string orderUserName = User.Identity?.Name ?? "system";

            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();
                using var tran = conn.BeginTransaction();

                // ==============================
                // 1. 品目マスタ TM_CONSUMABLES から ID と 単価 を取得
                // ==============================
                int consumablesId;
                int unitPrice;   // UNIT_PRICE は int

                using (var cmd = new SqlCommand(
                    @"SELECT ID, UNIT_PRICE 
                      FROM TM_CONSUMABLES 
                      WHERE ITEM_NAME = @ItemName
                        AND DELETE_FLAG = 0",
                    conn, tran))
                {
                    cmd.Parameters.AddWithValue("@ItemName", ItemName);

                    using var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        // 対応する品目がなければエラー表示して画面に戻す
                        ModelState.AddModelError(string.Empty, "品目が存在しません。");
                        return Page();
                    }

                    consumablesId = reader.GetInt32(0); // ID
                    unitPrice = reader.GetInt32(1); // UNIT_PRICE
                }

                // ==============================
                // 2. 親テーブル TD_ORDER に INSERT
                // ==============================
                int newOrderId;

                // INSERT文
                using (var cmd = new SqlCommand(@"
INSERT INTO TD_ORDER
    (ORDER_NO,
     ORDER_DATE,
     ORDER_USER_NAME,
     CONTACT_ORDER_NAME,
     ORDER_NOTE,
     ORDER_STATUS_ID)
OUTPUT INSERTED.ID
VALUES
    (@OrderNo,
     @OrderDate,
     @OrderUserName,
     @ContactOrderName,
     NULL,
     1);", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@OrderNo", orderNo);
                    cmd.Parameters.AddWithValue("@OrderDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@OrderUserName", orderUserName);
                    cmd.Parameters.AddWithValue("@ContactOrderName", orderUserName);

                    // INSERT された行の ID を取得
                    newOrderId = (int)cmd.ExecuteScalar();
                }

                // ==============================
                // 3. 子テーブル TD_ORDER_DETAILS に INSERT
                // ==============================
                using (var cmd = new SqlCommand(@"
INSERT INTO TD_ORDER_DETAILS
    (ORDER_ID,
     SEQ_NO,
     CONSUMABLES_ID,
     ORDER_QUANTITY,
     ORDER_UNIT_NAME,
     ORDERING_TIME_UNIT_PRICE,
     DELETE_FLAG)
VALUES
    (@OrderId,
     1,                -- 明細番号（今回は1件だけなので1固定）
     @ConsumablesId,
     @OrderQuantity,
     @OrderUnitName,
     @UnitPrice,
     0);
", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@OrderId", newOrderId);
                    cmd.Parameters.AddWithValue("@ConsumablesId", consumablesId);
                    cmd.Parameters.AddWithValue("@OrderQuantity", Quantity);

                    // ★ 単位名：今回は単位の概念を使わないので、固定で「なし」を入れる
                    cmd.Parameters.AddWithValue("@OrderUnitName", "なし");

                    cmd.Parameters.AddWithValue("@UnitPrice", unitPrice);

                    cmd.ExecuteNonQuery();
                }

                // 4. すべて成功したらコミット
                tran.Commit();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "登録中にエラーが発生しました。" + ex.Message);
                return Page();
            }

            // 正常終了 → 完了画面へ
            return RedirectToPage("/Complete");
        }
    }
}
