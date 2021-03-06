﻿using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace CostAccounting
{
    public partial class Form_Print_Product : Form
    {
        /*************************************************************
         * コンストラクタ
         *************************************************************/
        public Form_Print_Product()
        {
            InitializeComponent();
        }

        /*************************************************************
         * フォームロード時の処理
         *************************************************************/
        private void Form_Print_Product_Load(object sender, EventArgs e)
        {
            // 出力フォルダのデフォルトはアプリケーションの実行フォルダを指定
            outputDir.Text = Application.StartupPath;
            folderBrowserDialog.SelectedPath = Application.StartupPath;

            // 印刷対象データを表示する
            setTargetData();
        }

        /*************************************************************
         * リストビューに印刷対象データを表示する
         *************************************************************/
        private void setTargetData()
        {
            // 既に表示されているデータをクリアする
            listView.Items.Clear();

            // 出力条件を取得する
            Const.PRODUCT_TYPE productType = Program.judgeProductType(radioProduct, radioBlend);
            Const.CATEGORY_TYPE category = Program.judgeCategory(radioBudget, radioActual);

            string supplier = textSupplier.Text;
            string product = textProduct.Text;

            // 出力条件に従い、印刷対象データを検索し、リストビューに設定する
            using (var context = new CostAccountingEntities())
            {
                var target = from t in context.ProductSupplier
                             join m_supplier in context.Supplier on new { t.year, code = t.supplier_code } equals new { m_supplier.year, m_supplier.code }
                             join m_product in context.ProductCode on new { t.year, code = t.product_code } equals new { m_product.year, m_product.code }
                             where t.year.Equals(Const.TARGET_YEAR)
                                && t.type.Equals((int)productType)
                                && t.category.Equals((int)category)
                             orderby t.supplier_code, t.product_code
                             select new { t.supplier_code, supplier_name = m_supplier.name, t.product_code, product_name = m_product.name };

                foreach (var data in target.ToList())
                {
                    if (!string.IsNullOrEmpty(supplier) && !data.supplier_name.Contains(supplier))
                        continue;

                    if (!string.IsNullOrEmpty(product) && !data.product_name.Contains(product))
                        continue;

                    ListViewItem item = new ListViewItem(data.supplier_code);
                    item.SubItems.Add(data.supplier_name);
                    item.SubItems.Add(data.product_code);
                    item.SubItems.Add(data.product_name);
                    item.Checked = true;
                    listView.Items.Add(item);
                }

                recordCnt.Text = listView.CheckedItems.Count.ToString("#,0");
            }
        }

        /*************************************************************
        * ラジオボタンの状態に従い、印刷対象データの再検索を行う。
        *************************************************************/
        private void radio_CheckedChanged(object sender, EventArgs e)
        {
            // チェックされた方のイベントのみ処理を行う
            RadioButton radio = (RadioButton)sender;
            if (!radio.Checked)
                return;

            setTargetData();
        }

        /*************************************************************
         * 出力フォルダの変更ボタン押下時の処理
         *************************************************************/
        private void btnRefOutputDir_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                outputDir.Text = folderBrowserDialog.SelectedPath;
            }
        }

        /*************************************************************
         * 出力ボタン押下時の処理
         *************************************************************/
        private void btnOutput_Click(object sender, EventArgs e)
        {
            if (listView.CheckedItems.Count == 0)
            {
                Program.MessageBoxError("出力対象のレコードがありません。");
                return;
            }

            if (Program.MessageBoxBefore("出力条件の内容で" + listView.CheckedItems.Count + "件のデータをExcelファイルに出力しますか？") != DialogResult.Yes)
                return;

            progressBar.Minimum = 0;
            progressBar.Maximum = listView.CheckedItems.Count;
            progressBar.Value = 0;
            progressBar.Step = 1;

            // テンプレートのファイル
            var template = radioProduct.Checked ? @"\" + Properties.Resources.template_product
                                                : @"\" + Properties.Resources.template_blend;

            var templateFile = new FileInfo(string.Concat(System.Configuration.ConfigurationManager.AppSettings["templateFolder"], template));

            // 出力ファイル
            var outputFile = new FileInfo(string.Concat(Application.StartupPath
                                                        , radioProduct.Checked ? @"\商品帳票" : @"\ブレンド品帳票"
                                                        , radioBudget.Checked ? "【予定】_" : "【実績】_"
                                                        , DateTime.Now.ToString("yyyyMMddHHmmss")
                                                        , ".xlsx"));

            using (var package = new ExcelPackage(outputFile, templateFile))
            {
                ExcelWorksheet summarySheet = package.Workbook.Worksheets["summary"];
                ExcelWorksheet templateSheet = package.Workbook.Worksheets["template"];

                summarySheet.InsertRow(2, listView.CheckedItems.Count);

                for (int i = 0; i < listView.CheckedItems.Count; i++)
                {
                    ListViewItem item = listView.CheckedItems[i];
                    string supplierCode = item.SubItems[0].Text;
                    string supplierName = item.SubItems[1].Text;
                    string productCode = item.SubItems[2].Text;
                    string productName = item.SubItems[3].Text;
                    string sheetName = string.Concat(supplierCode, "-", productCode);

                    // summaryシートの設定
                    int targetRow = i + 2;
                    summarySheet.Cells[targetRow, 1].Value = (i + 1);
                    summarySheet.Cells[targetRow, 2].Value = supplierCode;
                    summarySheet.Cells[targetRow, 3].Value = supplierName;
                    summarySheet.Cells[targetRow, 4].Value = productCode;
                    summarySheet.Cells[targetRow, 5].Value = productName;
                    summarySheet.Cells[targetRow, 6].Style.Font.UnderLine = true;
                    summarySheet.Cells[targetRow, 6].Style.Font.Color.SetColor(Color.Blue);

                    // テンプレートシートをコピー
                    ExcelWorksheet targetSheet = package.Workbook.Worksheets.Add(string.Concat(supplierCode, "-", productCode), templateSheet);
                    addHyperLink(summarySheet, summarySheet.Cells[(i + 2), 6], targetSheet.Cells["A1"], sheetName);

                    // コピーしたシートに対して値を設定
                    if (radioProduct.Checked)
                        setProductData(targetSheet, supplierCode, supplierName, productCode, productName);
                    else if (radioBlend.Checked)
                        setBlendData(targetSheet, supplierCode, supplierName, productCode, productName);
                    else
                        Program.MessageBoxError("商品かブレンド品のラジオボタンを選択してください。");

                    // プログレスバーの値を更新
                    labelStatus.Refresh();
                    labelStatus.Text = string.Format("・・・ ( {0} / {1} )"
                                                     , (i + 1).ToString("#,0")
                                                     , listView.CheckedItems.Count.ToString("#,0"));

                    progressBar.PerformStep();
                }

                // テンプレートシートを削除の上、Excelファイルを保存する
                summarySheet.Calculate();
                package.Workbook.Worksheets.Delete(templateSheet);
                package.Workbook.Worksheets.First().Select();
                package.Save();
            }

            Logger.Info(Message.INF006, new string[] { this.Text, Message.create(outputDir, recordCnt) + outputFile.Name });
            Program.openExcel(outputFile.FullName);
        }

        /*************************************************************
         * ハイパーリンクのを設定
         *************************************************************/
        private void addHyperLink(ExcelWorksheet ws, ExcelRange source, ExcelRange destination, string displayText)
        {
            source.Formula = "HYPERLINK(\"[\"&MID(CELL(\"filename\"),SEARCH(\"[\",CELL(\"filename\"))+1, SEARCH(\"]\",CELL(\"filename\"))-SEARCH(\"[\",CELL(\"filename\"))-1)&\"]" + destination.FullAddress + "\",\"" + displayText + "\")";
        }

        /*************************************************************
         * 商品の原価計算データを設定
         *************************************************************/
        private void setProductData(ExcelWorksheet ws, string supplierCode, string supplierName, string productCode, string productName)
        {
            Const.CATEGORY_TYPE category = Program.judgeCategory(radioBudget, radioActual);

            ws.Cells["AH2"].Value = productName;
            ws.Cells["S2"].Value = supplierName;

            using (var context = new CostAccountingEntities())
            {
                var product = from t_product in context.Product
                              join t_supplier in context.ProductSupplier on
                                   new { t_product.year, t_product.code, t_product.category, t_product.type }
                                     equals
                                   new { t_supplier.year, code = t_supplier.product_code, t_supplier.category, t_supplier.type }
                              where t_product.year.Equals(Const.TARGET_YEAR)
                                 && t_product.code.Equals(productCode)
                                 && t_supplier.supplier_code.Equals(supplierCode)
                                 && t_product.category.Equals((int)category)
                                 && t_product.type.Equals((int)Const.PRODUCT_TYPE.Normal)
                              select new { t_product, t_supplier };

                ws.Cells["S3"].Value = product.First().t_product.packing;
                ws.Cells["AR2"].Value = product.First().t_product.volume;
                ws.Cells["AZ2"].Value = product.First().t_product.tray_num;
                ws.Cells["L3"].Value = product.First().t_supplier.unit_price;
                ws.Cells["P31"].Value = product.First().t_product.note;
                ws.Cells["G35"].Value = product.First().t_supplier.update_user;
                ws.Cells["G36"].Value = product.First().t_supplier.update_date;

                ws.Cells["R7"].Value = Parameters.getInstance(category).rateLoss;
                ws.Cells["T20"].Value = Parameters.getInstance(category).allocationSale;
                ws.Cells["W21"].Value = Parameters.getInstance(category).allocationMng;
                ws.Cells["U22"].Value = Parameters.getInstance(category).allocationExt;

                // ①原料費の設定------------------------------------------------------------
                var material = from t in context.ProductMaterial
                               join m in context.RowMaterial on
                                    new { t.year, t.code } equals new { m.year, m.code }
                               where t.year.Equals(Const.TARGET_YEAR)
                                  && t.product_code.Equals(productCode)
                                  && t.category.Equals((int)category)
                               orderby t.no
                               select new { t.code, t.quantity, m.name };

                int startRow = 4;
                foreach (var data in material.ToList())
                {
                    ws.Cells["AB" + startRow].Value = data.name;
                    ws.Cells["AI" + startRow].Value = data.quantity;
                    ws.Cells["AM" + startRow].Value = DataTableSupport.getPrice(DataTableSupport.getInstance(category).rowMaterial, data.code);
                    ++startRow;
                }

                // ②外注費の設定------------------------------------------------------------
                var contractor = from t in context.ProductContractor
                                 where t.year.Equals(Const.TARGET_YEAR)
                                    && t.product_code.Equals(productCode)
                                    && t.category.Equals((int)category)
                                 orderby t.no
                                 select t;
                startRow = 20;
                foreach (var data in contractor.ToList())
                {
                    ws.Cells["AB" + startRow].Value = data.name;
                    ws.Cells["AI" + startRow].Value = data.cost;
                    ws.Cells["AL" + startRow].Value = data.quantity;
                    ++startRow;
                }

                // ③労務費の設定------------------------------------------------------------
                ws.Cells["AG28"].Value = product.First().t_product.preprocess_time_m;
                ws.Cells["AI29"].Value = product.First().t_product.night_time_m;
                ws.Cells["AK28"].Value = product.First().t_product.dry_time_m;
                ws.Cells["AM28"].Value = product.First().t_product.selection_time_m;
                ws.Cells["AG30"].Value = product.First().t_product.preprocess_time_f;
                ws.Cells["AI30"].Value = product.First().t_product.night_time_f;
                ws.Cells["AK30"].Value = product.First().t_product.dry_time_f;
                ws.Cells["AM30"].Value = product.First().t_product.selection_time_f;

                ws.Cells["AR28"].Value = Parameters.getInstance(category).wageM;
                ws.Cells["AR29"].Value = Parameters.getInstance(category).wageIndirect;
                ws.Cells["AR30"].Value = Parameters.getInstance(category).wageF;
                ws.Cells["AO32"].Value = Parameters.getInstance(category).trayNum;

                // ④製造経費の設定------------------------------------------------------------
                // 《原料運賃》
                var materialFare = from t in context.ProductMaterialsFare
                                   where t.year.Equals(Const.TARGET_YEAR)
                                      && t.product_code.Equals(productCode)
                                      && t.category.Equals((int)category)
                                   orderby t.no
                                   select t;
                startRow = 36;
                foreach (var data in materialFare.ToList())
                {
                    ws.Cells["AB" + startRow].Value = data.name;
                    ws.Cells["AK" + startRow].Value = data.quantity;
                    ws.Cells["AO" + startRow].Value = data.cost;
                    ++startRow;
                }

                // 《包装資材費》
                var packing = from t in context.ProductPacking
                              join m in context.Material on
                                   new { t.year, t.code } equals new { m.year, m.code }
                              where t.year.Equals(Const.TARGET_YEAR)
                                 && t.product_code.Equals(productCode)
                                 && t.category.Equals((int)category)
                              orderby t.no
                              select new { t.code, t.quantity, m.name };

                startRow = 44;
                foreach (var data in packing.ToList())
                {
                    ws.Cells["AB" + startRow].Value = data.name;
                    ws.Cells["AK" + startRow].Value = data.quantity;
                    ws.Cells["AO" + startRow].Value = DataTableSupport.getPrice(DataTableSupport.getInstance(category).material, data.code);
                    ++startRow;
                }

                // 《設備費》
                var machine = from t in context.ProductMachine
                              join m in context.Machine on
                                   new { t.year, t.code } equals new { m.year, m.code }
                              where t.year.Equals(Const.TARGET_YEAR)
                                 && t.product_code.Equals(productCode)
                                 && t.category.Equals((int)category)
                              orderby t.no
                              select new { t.code, t.time, m.name };
                startRow = 56;
                foreach (var data in machine.ToList())
                {
                    ws.Cells["AB" + startRow].Value = string.Concat(data.code, " ", data.name);
                    ws.Cells["AK" + startRow].Value = data.time;
                    ws.Cells["AO" + startRow].Value = DataTableSupport.getPrice(DataTableSupport.getInstance(category).machine, data.code);
                    ++startRow;
                }
                ws.Cells["AI56"].Value = product.First().t_product.tray_num;

                // 《水道光熱費》
                ws.Cells["AL70"].Value = Parameters.getInstance(category).utilitiesAD;
                ws.Cells["AL71"].Value = Parameters.getInstance(category).utilitiesFD;

                // 《その他経費》
                ws.Cells["AL76"].Value = Parameters.getInstance(category).allocationFD;
                ws.Cells["AL77"].Value = Parameters.getInstance(category).allocationAD;
                ws.Cells["AL78"].Value = Parameters.getInstance(category).allocationLabor;

                // 《荷造運賃》
                var packingFare = from t in context.ProductPackingFare
                                  join m in context.Fare on
                                       new { t.year, t.code } equals new { m.year, m.code }
                                  where t.year.Equals(Const.TARGET_YEAR)
                                     && t.product_code.Equals(productCode)
                                     && t.supplier_code.Equals(supplierCode)
                                     && t.category.Equals((int)category)
                                  orderby t.no
                                  select new { t.code, t.quantity, m.name };
                startRow = 84;
                foreach (var data in packingFare.ToList())
                {
                    ws.Cells["AB" + startRow].Value = data.name;
                    ws.Cells["AK" + startRow].Value = data.quantity;
                    ws.Cells["AO" + startRow].Value = DataTableSupport.getPrice(DataTableSupport.getInstance(category).fare, data.code);
                    ++startRow;
                }
            }

            ws.Calculate();
        }

        /*************************************************************
         * ブレンド品の原価計算データを設定
         *************************************************************/
        private void setBlendData(ExcelWorksheet ws, string supplierCode, string supplierName, string productCode, string productName)
        {
            Const.CATEGORY_TYPE category = Program.judgeCategory(radioBudget, radioActual);

            ws.Cells["E2"].Value = productName;
            ws.Cells["S2"].Value = supplierName;

            using (var context = new CostAccountingEntities())
            {
                var product = from t_product in context.Product
                              join t_supplier in context.ProductSupplier on
                                   new { t_product.year, t_product.code, t_product.category, t_product.type }
                                     equals
                                   new { t_supplier.year, code = t_supplier.product_code, t_supplier.category, t_supplier.type }
                              where t_product.year.Equals(Const.TARGET_YEAR)
                                 && t_product.code.Equals(productCode)
                                 && t_supplier.supplier_code.Equals(supplierCode)
                                 && t_product.category.Equals((int)category)
                                 && t_product.type.Equals((int)Const.PRODUCT_TYPE.Blend)
                              select new { t_product, t_supplier };

                ws.Cells["S3"].Value = product.First().t_product.packing;
                ws.Cells["L3"].Value = product.First().t_supplier.unit_price;
                ws.Cells["P31"].Value = product.First().t_product.note;
                ws.Cells["G35"].Value = product.First().t_supplier.update_user;
                ws.Cells["G36"].Value = product.First().t_supplier.update_date;

                // ブレンド品の設定------------------------------------------------------------
                var blend = from t in
                                (from t_product in context.Product
                                 join t_blend in context.ProductBlend
                                 on new { t_product.year, t_product.code, t_product.category }
                                    equals
                                    new { t_blend.year, code = t_blend.product_code, t_blend.category }
                                 where t_product.year.Equals(Const.TARGET_YEAR)
                                    && t_product.code.Equals(productCode)
                                    && t_product.category.Equals((int)category)
                                    && t_product.type.Equals((int)Const.PRODUCT_TYPE.Blend)
                                 select new { product = t_product, blend = t_blend }
                                )
                            join m in context.ProductCode on
                            new { t.blend.year, t.blend.code } equals new { m.year, m.code }
                            orderby t.blend.no
                            select new { t.product, t.blend, m.name };

                var blendList = blend.ToList();
                int startColumm = 14;
                int add = 5;

                for (int i = 0; i < blendList.Count; i++)
                {
                    var data = blendList[i];
                    ws.Cells[5, startColumm + (add * i)].Value = data.blend.blend_rate;
                    ws.Cells[6, startColumm + (add * i)].Value = data.name;
                    ws.Cells[7, startColumm + (add * i)].Value = data.product.material_cost;
                    ws.Cells[8, startColumm + (add * i)].Value = data.product.contractors_cost;
                    ws.Cells[9, startColumm + (add * i)].Value = data.product.labor_cost;
                    ws.Cells[10, startColumm + (add * i)].Value = data.product.labor_cost_direct;
                    ws.Cells[11, startColumm + (add * i)].Value = data.product.labor_cost_indirect;
                    ws.Cells[12, startColumm + (add * i)].Value = data.product.manufacturing_cost;
                    ws.Cells[13, startColumm + (add * i)].Value = data.product.materials_fare;
                    ws.Cells[14, startColumm + (add * i)].Value = data.product.packing_cost;
                    ws.Cells[15, startColumm + (add * i)].Value = data.product.machine_cost;
                    ws.Cells[16, startColumm + (add * i)].Value = data.product.utilities_cost;
                    ws.Cells[17, startColumm + (add * i)].Value = data.product.other_cost;
                    ws.Cells[18, startColumm + (add * i)].Value = data.product.product_cost;
                    ws.Cells[19, startColumm + (add * i)].Value = data.product.packing_fare;
                    ws.Cells[20, startColumm + (add * i)].Value = data.product.selling_cost;
                    ws.Cells[21, startColumm + (add * i)].Value = data.product.management_cost;
                    ws.Cells[23, startColumm + (add * i)].Value = data.product.overall_cost;
                }
            }
            ws.Calculate();
        }

        /*************************************************************
         * チェックボックスの状態を変更する
         *************************************************************/
        private void changeCheckBoxState(bool state)
        {
            listView.ItemChecked -= new ItemCheckedEventHandler(listView_ItemChecked);
            foreach (ListViewItem item in listView.Items)
                item.Checked = state;
            listView.ItemChecked += new ItemCheckedEventHandler(listView_ItemChecked);
 
            recordCnt.Text = listView.CheckedItems.Count.ToString("#,0");
        }

        /*************************************************************
         * リストビューのチェックボックスのチェック状態変更時の処理
         *************************************************************/
        private void listView_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            recordCnt.Text = listView.CheckedItems.Count.ToString("#,0");
        }

        /*************************************************************
         * リストビューのチェックボックスを全てONにする
         *************************************************************/
        private void btnAllCheck_Click(object sender, EventArgs e)
        {
            changeCheckBoxState(true);
        }

        /*************************************************************
         * リストビューのチェックボックスを全てOFFにする
         *************************************************************/
        private void btnAllClear_Click(object sender, EventArgs e)
        {
            changeCheckBoxState(false);
        }

        /*************************************************************
         * 取引先略称と商品名のあいまい検索（Enterキー押下時）
         *************************************************************/
        private void textSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                setTargetData();
            }
        }

    }
}
