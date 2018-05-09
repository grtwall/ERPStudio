﻿using ERPFramework.Controls;
using ERPFramework.Data;
using ERPFramework.DataGridViewControls;
using ERPFramework.Forms;
using ERPFramework.Libraries;
using MetroFramework.Controls;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Windows.Forms;

namespace ERPFramework.CounterManager
{
    public partial class codesForm : DocumentForm
    {
        private ComboBoxManager cbbManager;
        private EnumsManager<InputType> eSegmentType;

        public ComboBox GetCode { get { return cbbCode; } }

        public MetroTextBox GetDescription { get { return txtDescription; } }

        public ExtendedDataGridView GetDataGrid { get { return dgwSegments; } }

        public ComboBoxManager GetCbbManager { get { return cbbManager; } }

        public codesForm()
            : base("codes")
        {
            InitializeComponent();
            InitializeAuxComponent();
        }

        #region Virtual Method

        protected override void OnInitializeData()
        {
            dbManager = new dbmanagerCodes("codesForm", this);
            dbManager.AttachRadar<RadarCodes>();
            dbManager.AddMaster<AM_Codes>();
            dbManager.AddSlave<AM_CodeSegment>();
            dbManager.AddRelation("CodeSegment", AM_Codes.CodeType, AM_CodeSegment.CodeType);

            dgwSegments.AutoGenerateColumns = false;
            dgwSegments.DataSource = dbManager.SlaveBinding("CodeSegment");

            eSegmentType = new EnumsManager<InputType>(sgmCodeType as MetroDataGridViewComboBoxColumn, "");
            //sgmCodeType.Items.AddRange(1, 2, 3);


            //sgmSegmentNo.DataPropertyName = AM_CodeSegment.Segment.Name;
            // sgmLength.DataPropertyName = AM_CodeSegment.InputLen.Name;
            //sgmHeader.DataPropertyName = AM_CodeSegment.Description.Name;
        }

        protected override void OnBindData()
        {
            BindControl(cbbCode, AM_Codes.CodeType, "SelectedValue", NullValue.NotSet);
            BindControl(txtDescription, AM_Codes.Description);
            BindControl(dgwSegments);

            BindColumn(sgmSegmentNo, AM_CodeSegment.Segment);
            BindColumn(sgmCodeType, AM_CodeSegment.InputType);
            BindColumn(sgmLength, AM_CodeSegment.InputLen);
            BindColumn(sgmHeader, AM_CodeSegment.Description);
            sgmCodeType.DataPropertyName = "Archive";


            dgwSegments.RowIndex = AM_CodeSegment.Segment;
        }

        protected override void OnDisableControlsForEdit()
        {
            cbbCode.Enabled = false;
            txtDescription.Enabled = false;
            base.OnDisableControlsForEdit();
        }

        protected override void OnDisableControlsForNew()
        {
        }

        protected override void OnPrepareAuxData()
        {
        }

        protected override void FocusOnNew()
        {
            cbbCode.Focus();
        }

        protected override bool OnBeforeSave()
        {
            return base.OnBeforeSave();
        }

        #endregion

        public int AddNewRow(string desc, int inputType, int inputLen)
        {
            var drw = dgwSegments.AddNewRow(AM_CodeSegment.Segment);

            drw.SetValue<string>(AM_CodeSegment.Description, desc);
            drw.SetValue<int>(AM_CodeSegment.InputType, inputType);
            drw.SetValue<int>(AM_CodeSegment.InputLen, inputLen);

            return drw.GetValue<int>(AM_CodeSegment.Segment);
        }

        private void InitializeAuxComponent()
        {
            cbbManager = new ComboBoxManager();
            cbbManager.CreateList<string>();
            foreach (KeyValuePair<string, string> codes in GlobalInfo.CodeTypes)
                cbbManager.AddValue(codes.Key, codes.Value);
            cbbManager.AttachTo(cbbCode);
            //eSegmentType = new EnumsManager<InputType>(sgmCodeType, Properties.Resources.ResourceManager);
        }

        private void cbbCode_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtDescription.Text = cbbCode.Text;
        }
    }

    #region dbmanagerCodes

    internal class dbmanagerCodes : ERPFramework.Data.DBManager
    {
        public dbmanagerCodes(string name,DocumentForm document)
            : base(name, document)
        { }

        protected override void dAdapter_MasterRowUpdating(object sender, RowUpdatingEventArgs e)
        {
            base.dAdapter_RowUpdating(sender, e);
        }

        protected override void dAdapter_RowUpdating(object sender, RowUpdatingEventArgs e)
        {
            if (e.StatementType == StatementType.Insert)
            {
                e.Row[AM_CodeSegment.CodeType.Name] = GetColumn<string>(AM_Codes.CodeType, Pos);
            }
            base.dAdapter_RowUpdating(sender, e);
        }

        protected override string CreateMasterQuery(ref List<SqlProxyParameter> dParam)
        {
            QueryBuilder qb = new QueryBuilder();
            qb.SelectAllFrom<AM_Codes>();
            qb.Where(AM_Codes.CodeType).IsEqualTo(dParam[0]);

            return qb.Query;
        }

        protected override List<SqlProxyParameter> CreateMasterParam()
        {
            List<SqlProxyParameter> PList = new List<SqlProxyParameter>();

            SqlProxyParameter nParam = new SqlProxyParameter("@p1", AM_Codes.CodeType);
            nParam.Value = 0;
            PList.Add(nParam);

            return PList;
        }

        protected override void SetParameters(IRadarParameters key, DBCollection collection)
        {
            collection.Parameter[0].Value = key[0];
        }

        protected override string CreateSlaveQuery(string name, List<SqlProxyParameter> dParam)
        {
            if (name == AM_CodeSegment.Name)
            {
                QueryBuilder qb = new QueryBuilder().
                       SelectAllFrom<AM_CodeSegment>().
                        Where(AM_CodeSegment.CodeType).IsEqualTo(dParam[0]).
                        OrderBy(AM_CodeSegment.Segment);

                return qb.Query;
            }

            return "";
        }

        protected override List<SqlProxyParameter> CreateSlaveParam(string name)
        {
            if (name == AM_CodeSegment.Name)
            {
                List<SqlProxyParameter> PList = new List<SqlProxyParameter>();

                SqlProxyParameter sParam = new SqlProxyParameter("@p2", AM_CodeSegment.CodeType);
                sParam.Value = 0;
                PList.Add(sParam);
                return PList;
            }
            return null;
        }
    }

    #endregion
}