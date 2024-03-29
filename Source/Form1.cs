﻿
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MasterConverterGUI
{
    public partial class Form1 : Form
    {
        //----- params -----

        //----- field -----

        private Model model = null;

        //----- property -----

        //----- method -----        

        public Form1()
        {
            InitializeComponent();

            model = new Model();

            //------ イベント購読 ------

            // マスター一覧更新イベント.
            model.OnUpdateMastersAsObservable().Subscribe(x => RefreshListView(x));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 初期サイズが最小サイズ.
            MinimumSize = Size;

            LoadConfig();

            //------ Model初期化 ------

            model.Initialize();

            //------ View初期化 ------

            InitializeListView();
            InitializeComboBox();

            //------ ダブルバッファリング ------

            EnableDoubleBuffer(listView1);
            EnableDoubleBuffer(richTextBox1);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveConfig();
        }
        
        // 設定を読み込み.
        private void LoadConfig()
        {
            var settings = Properties.Settings.Default;

            if (settings.Size.Width != 0 || settings.Size.Height != 0)
            {
                Location = settings.Location;
                Size = settings.Size;
            }

            model.SearchDirectory = settings.SearchDirectory;
            model.Tags = settings.Tags;
        }

        // 設定を保存.
        private void SaveConfig()
        {
            var settings = Properties.Settings.Default;

            if (WindowState == FormWindowState.Normal)
            {
                settings.Location = Location;
                settings.Size = Size;
            }
            else
            {
                settings.Location = RestoreBounds.Location;
                settings.Size = RestoreBounds.Size;
            }

            settings.SearchDirectory = model.SearchDirectory;
            settings.Tags = model.Tags;

            settings.Save();
        }

        private void SetControlEnable(bool state)
        {
            comboBox1.Enabled = state;
            ExecButton.Enabled = state;
            select_all.Enabled = state;
            select_all.Enabled = state;
        }

        //------ コンボボックスコントロール制御 ------

        private void InitializeComboBox()
        {
            comboBox1.Items.AddRange(Enum.GetNames(typeof(Mode)));

            comboBox1.SelectedIndex = (int)Mode.Import;
        }

        private void comboBox1_SelectionChangeCommitted(object sender, EventArgs e)
        {
            model.Mode = (Mode)comboBox1.SelectedIndex;

            richTextBox1.Text = string.Empty;
        }

        //------ ログテキストビューコントロール制御 ------

        delegate void TextBoxDeledate(string text);

        private void SetLogText(string text)
        {
            if (richTextBox1.InvokeRequired)
            {
                // 呼び出しスレッドとスレッドIDが異なる場合
                TextBoxDeledate temp_del = new TextBoxDeledate(SetLogText);

                Invoke(temp_del, new object[] { text });
            }
            else
            {
                // 呼び出しスレッドとスレッドIDが一致している場合、直接フォームコントロールに設定.
                richTextBox1.Text = text;
            }
        }

        //------ プログレスバーコントロール制御 ------

        delegate void ProgressbarDeledate(int value);

        private void SetProgress(int value)
        {
            if (richTextBox1.InvokeRequired)
            {
                // 呼び出しスレッドとスレッドIDが異なる場合
                ProgressbarDeledate temp_del = new ProgressbarDeledate(SetProgress);

                Invoke(temp_del, new object[] { value });
            }
            else
            {
                // 呼び出しスレッドとスレッドIDが一致している場合、直接フォームコントロールに設定.
                progressBar1.Value = value;
            }
        }

        //------ リストビューコントロール制御 ------

        private bool checkChangeCancel = false;

        private void InitializeListView()
        {
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.CheckBoxes = true;
            listView1.OwnerDraw = true;
            listView1.Sorting = SortOrder.None;
            listView1.View = View.Details;
            listView1.HeaderStyle = ColumnHeaderStyle.None;
            
            listView1.ItemCheck += OnItemCheck;
            listView1.MouseDown += OnItemMouseDown;
            listView1.MouseDoubleClick += OnItemDoubleClick;

            // 列（コラム）ヘッダの作成. 
            var masterNameColumn = new ColumnHeader() { Text = "Master", Width = 200 };
            var masterDirectoryColumn = new ColumnHeader() { Text = "Directory", Width = -2 };

            ColumnHeader[] colHeaderRegValue = { masterNameColumn, masterDirectoryColumn };

            listView1.Columns.AddRange(colHeaderRegValue);

            SizeLastColumn(listView1);
        }

        private void listView1_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(SystemBrushes.Menu, e.Bounds);
            e.Graphics.DrawRectangle(SystemPens.GradientInactiveCaption,
                                     new Rectangle(e.Bounds.X, 0, e.Bounds.Width, e.Bounds.Height));

            if (e.ColumnIndex < listView1.Columns.Count)
            {
                var text = listView1.Columns[e.ColumnIndex].Text;
                TextFormatFlags cFlag = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
                TextRenderer.DrawText(e.Graphics, text, listView1.Font, e.Bounds, Color.Black, cFlag);
            }
        }

        private void listView1_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void listView1_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void listView1_Resize(object sender, System.EventArgs e)
        {
            SizeLastColumn((ListView)sender);
        }

        private void RefreshListView(Model.MasterInfo[] masters)
        {
            listView1.BeginUpdate();

            listView1.Items.Clear();

            foreach (var master in masters)
            {
                var item = new ListViewItem();

                item.Text = master.masterName;
                item.SubItems.Add(master.localPath);
                item.Checked = master.selection;

                listView1.Items.Add(item);
            }

            listView1.EndUpdate();
        }

        private void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            // 変更却下状態時はチェック変更をキャンセル.
            if (checkChangeCancel)
            {
                e.NewValue = e.NewValue == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked;
            }

            var masterInfo = model.CurrentMasterInfos[e.Index];

            if (e.NewValue == CheckState.Unchecked)
            {
                masterInfo.selection = false;
            }
            else if (e.NewValue == CheckState.Checked)
            {
                masterInfo.selection = true;
            }

            model.UpdateMasterInfo(masterInfo);
        }

        private void OnItemMouseDown(object sender, MouseEventArgs e)
        {
            // マウスボタンが押された回数を判定.
            checkChangeCancel = e.Clicks == 2;

            var item = listView1.GetItemAt(e.X, e.Y);

            if (item != null)
            {
                var rect = GetCheckBoxRectangle(listView1, item.Index);

                // チェックボックスの場所でマウスが押された場合のみCheckBoxを反転させる.
                checkChangeCancel &= !rect.Contains(e.Location);

                if (item.Selected)
                {
                    item.Selected = true;
                }
            }
        }

        private void OnItemDoubleClick(object sender, MouseEventArgs e)
        {
            var item = listView1.GetItemAt(e.X, e.Y);

            if (item != null)
            {
                var checkRect = GetCheckBoxRectangle(listView1, item.Index);

                var masterNameRect = GetMasterNameRectangle(listView1, item.Index);

                var masterInfo = model.CurrentMasterInfos[item.Index];

                if (!checkRect.Contains(e.Location))
                {
                    if (masterNameRect.Contains(e.Location))
                    {
                        model.OpenMasterXlsx(masterInfo);
                    }
                    else
                    {
                        model.OpenMasterDirectory(masterInfo);
                    }
                }

            }

            checkChangeCancel = false;
        }

        private static Rectangle GetCheckBoxRectangle(ListView listView, int itemIndex)
        {
            var rectSize = new Size(16, 16);

            var bounds = listView.GetItemRect(itemIndex);

            var y = bounds.Y + (bounds.Height - rectSize.Height) / 2;
            
            return new Rectangle(new Point(0, y), rectSize);
        }

        private static Rectangle GetMasterNameRectangle(ListView listView, int itemIndex)
        {
            var rectSize = new Size(16 + 200, 16);
            var xOffset = 16;

            var bounds = listView.GetItemRect(itemIndex);

            var y = bounds.Y + (bounds.Height - rectSize.Height) / 2;

            return new Rectangle(new Point(xOffset, y), rectSize);
        }

        private void SizeLastColumn(ListView listView)
        {
            if (1 < listView.Columns.Count)
            {
                listView.Columns[listView.Columns.Count - 1].Width = -2;
            }
        }

        //------ 一括選択・解除ボタン制御 ------

        private void SelectAllButton_Click(object sender, EventArgs e)
        {
            foreach (var masterInfo in model.CurrentMasterInfos)
            {
                masterInfo.selection = true;

                model.UpdateMasterInfo(masterInfo);
            }

            RefreshListView(model.CurrentMasterInfos);
        }

        private void UnSelectAllButton_Click(object sender, EventArgs e)
        {
            foreach (var masterInfo in model.CurrentMasterInfos)
            {
                masterInfo.selection = false;

                model.UpdateMasterInfo(masterInfo);
            }

            RefreshListView(model.CurrentMasterInfos);
        }

        //------ 検索テキストボックス制御 ------

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            model.UpdateSearchText(textBox1.Text);

            RefreshListView(model.CurrentMasterInfos);
        }

        //------ ドラッグアンドドロップ制御 ------

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            var path = files.FirstOrDefault();

            if (!string.IsNullOrEmpty(path))
            {
                model.Register(path);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        // 保存先フォルダ選択ダイアログ.
        private string OpenSaveFolderBrowserDialog(string selectionDirectory)
        {
            var path = string.Empty;

            var fbd = new FolderBrowserDialog();

            fbd.Description = "フォルダを指定してください。";

            if (string.IsNullOrEmpty(selectionDirectory) && Directory.Exists(selectionDirectory))
            {
                fbd.SelectedPath = selectionDirectory;
            }

            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                path = fbd.SelectedPath;
            }

            return path;
        }

        //------ 実行ボタン制御 ------

        private async void ExecButton_Click(object sender, EventArgs e)
        {
            var exec = true;

            switch (model.Mode)
            {
                case Mode.Import:
                    {
                        var caption = "インポート";
                        var message = "レコード情報からマスターを再構築しますか？\n※出力されていないデータは破棄されます";

                        var confirm = MessageBox.Show(message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        exec = confirm == DialogResult.Yes;
                    }
                    break;
            }

            if (exec)
            {
                richTextBox1.Focus();

                SetControlEnable(false);

                await ExecMasterConverter();

                SetControlEnable(true);
            }
        }

        //------ マスターコンバーター実行制御 ------

        private async Task ExecMasterConverter()
        {
            var success = 0;
            var progress = 0;

            var logBuilder = new StringBuilder();

            var masterInfos = model.MasterInfos.Where(x => x.selection).ToArray();

            richTextBox1.Text = string.Empty;

            progressBar1.Minimum = 0;
            progressBar1.Maximum = masterInfos.Length;
            progressBar1.Value = 0;

            Action<bool, string> onExecFinish = (result, log) =>
            {
                if (result)
                {
                    success++;
                }

                lock (logBuilder)
                {
                    logBuilder.AppendLine(log);
                }

                progress++;

                SetLogText(logBuilder.ToString());
                SetProgress(progress);
            };

            await model.ConvertMaster(onExecFinish);

            progressBar1.Value = 0;

            if (success == masterInfos.Length)
            {
                var caption = "Convert Complete";
                var message = string.Format("Convert {0} masters finish.", success);

                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var caption = "Convert Error";
                var message = string.Format("Convert Failed {0} masters.", masterInfos.Length - success);

                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void EnableDoubleBuffer(Control c)
        {
            var prop = c.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

            prop.SetValue(c, true, null);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
