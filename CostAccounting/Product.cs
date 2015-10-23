//------------------------------------------------------------------------------
// <auto-generated>
//     このコードはテンプレートから生成されました。
//
//     このファイルを手動で変更すると、アプリケーションで予期しない動作が発生する可能性があります。
//     このファイルに対する手動の変更は、コードが再生成されると上書きされます。
// </auto-generated>
//------------------------------------------------------------------------------

namespace CostAccounting
{
    using System;
    using System.Collections.Generic;
    
    public partial class Product
    {
        public int year { get; set; }
        public string code { get; set; }
        public int category { get; set; }
        public string item_code { get; set; }
        public decimal volume { get; set; }
        public string packing { get; set; }
        public string note { get; set; }
        public decimal material_cost { get; set; }
        public decimal labor_cost { get; set; }
        public decimal labor_cost_direct { get; set; }
        public decimal labor_cost_indirect { get; set; }
        public decimal contractors_cost { get; set; }
        public decimal manufacturing_cost { get; set; }
        public decimal materials_fare { get; set; }
        public decimal packing_cost { get; set; }
        public decimal machine_cost { get; set; }
        public decimal utilities_cost { get; set; }
        public decimal other_cost { get; set; }
        public decimal product_cost { get; set; }
        public decimal packing_fare { get; set; }
        public decimal selling_cost { get; set; }
        public decimal management_cost { get; set; }
        public decimal overall_cost { get; set; }
        public decimal preprocess_time_m { get; set; }
        public decimal preprocess_time_f { get; set; }
        public decimal night_time_m { get; set; }
        public decimal night_time_f { get; set; }
        public decimal dry_time_m { get; set; }
        public decimal dry_time_f { get; set; }
        public decimal selection_time_m { get; set; }
        public decimal selection_time_f { get; set; }
        public decimal tray_num { get; set; }
        public string update_user { get; set; }
        public System.DateTime update_date { get; set; }
        public string del_flg { get; set; }
    }
}
