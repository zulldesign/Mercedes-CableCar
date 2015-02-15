﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;
using System.Xml.Linq;
using Composite.Data;
using Composite.C1Console.Forms;
using Composite.Functions;
using Composite.Functions.ManagedParameters;
using Composite.Core.Types;
using Composite.Core.WebClient;
using Composite.Core.WebClient.FunctionCallEditor;
using Composite.Core.WebClient.State;
using Composite.Core.Xml;
using Composite.Core.ResourceSystem;
using Composite.Core.WebClient.UiControlLib;
using Composite.Core.Extensions;


namespace Composite.controls.FormsControls.FormUiControlTemplates.DeveloperTools
{

    public partial class FunctionParameterEditor : XhtmlPage
    {
        private static readonly string SessionStateProviderQueryKey = "StateProvider";
        private static readonly string StateIdQueryKey = "Handle";

        private const string _defaultFieldNamePrefix = "NewField";
        private bool nameChanged = false;


        private List<ManagedParameterDefinition> Parameters { get; set; }
        private List<Type> TypeOptions { get; set; }

        private IParameterEditorState _state;

        private Guid? _stateId;
        private Guid StateId
        {
            get
            {
                if (_stateId == null)
                {
                    string stateIdStr = Request.QueryString[StateIdQueryKey];

                    Guid stateId;
                    if (!SessionStateProviderName.IsNullOrEmpty()
                        && !stateIdStr.IsNullOrEmpty()
                        && Guid.TryParse(stateIdStr, out stateId))
                    {
                        _stateId = stateId;
                    }
                    else
                    {
                        _stateId = Guid.Empty;
                    }
                }

                return _stateId.Value;
            }
        }


        private string SessionStateProviderName
        {
            get
            {
                return Request.QueryString[SessionStateProviderQueryKey];
            }
        }

        protected string GetString(string localPart)
        {
            return StringResourceSystemFacade.GetString("Composite.Web.FormControl.FunctionParameterDesigner", localPart);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            LoadData();

            if (!IsPostBack)
            {
                InitializeViewState();
            }

            if (DetailsSplitPanelPlaceHolder.Visible == false && this.CurrentlySelectedFieldId != Guid.Empty)
            {
                InitializeDetailsSplitPanel();
            }

            if (Page.IsPostBack == false)
            {
                DetailsSplitPanelPlaceHolder.Visible = false;
                UpdateTypeList();
            }

            if (this.ViewState["Fields"] == null)
            {
                this.ViewState.Add("Fields", new List<ManagedParameterDefinition>());
                this.ViewState.Add("editedPatameterId", null);
            }

            if (Page.IsPostBack
                && this.Request.Form["__EVENTTARGET"] == string.Empty
                && CurrentlySelectedFieldId != Guid.Empty
                && ValidateSave())
            {
                Field_Save();
            }
        }

        public void OnMessage()
        {
            string message = ctlFeedback.GetPostedMessage();

            if(message == "save" || message == "persist" )
            {
                bool success = ValidateSave();
                ctlFeedback.SetStatus(success);

                if(success)
                {
                    Field_Save();
                }
            }
        }

        private void LoadData()
        {
            var provider = SessionStateManager.GetProvider(SessionStateProviderName);

            Verify.IsTrue(provider.TryGetState(StateId, out _state), "Failed to get session state");

            var parameters = _state.Parameters;
            var typeOptions = _state.ParameterTypeOptions; 

            Verify.IsNotNull(parameters, "Failed to get 'Parameters' binding from related workflow");
            Verify.IsNotNull(typeOptions, "Failed to get 'ParameterTypeOptions' binding from related workflow");

            Parameters = new List<ManagedParameterDefinition>(parameters);
            TypeOptions = new List<Type>(typeOptions);
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            if(CurrentlySelectedFieldId != Guid.Empty)
            {
                var defaultFunction = StandardFunctions.GetDefaultFunctionByType(this.CurrentlySelectedType);

                btnDefaultValueFunctionMarkup.Attributes["label"] = GetString(btnDefaultValueFunctionMarkup.Value.IsNullOrEmpty() ? "DefaultValueSpecify" : "DefaultValueEdit");
                btnDefaultValueFunctionMarkup.Attributes["url"] =
                    "${root}/content/dialogs/functions/editFunctionCall.aspx?type=" + this.CurrentlySelectedType.FullName +
                    "&dialoglabel=" + HttpUtility.UrlEncode(GetString("DefaultValueDialogLabel"), Encoding.UTF8) + "&multimode=false&functionmarkup=";


                btnTestValueFunctionMarkup.Attributes["label"] = GetString(btnTestValueFunctionMarkup.Value.IsNullOrEmpty() ? "TestValueSpecify" : "TestValueEdit");
                btnTestValueFunctionMarkup.Attributes["url"] =
                    "${root}/content/dialogs/functions/editFunctionCall.aspx?type=" + this.CurrentlySelectedType.FullName +
                    "&dialoglabel=" + HttpUtility.UrlEncode(GetString("TestValueDialogLabel"), Encoding.UTF8) + "&multimode=false&functionmarkup=";

                btnWidgetFunctionMarkup.Attributes["label"] = CurrentlySelectedWidgetText;
                btnWidgetFunctionMarkup.Attributes["url"] =
                    "${root}/content/dialogs/functions/editFunctionCall.aspx?functiontype=widget&type=" + this.CurrentlySelectedWidgetReturnType.FullName +
                    "&dialoglabel=" + HttpUtility.UrlEncode(GetString("WidgetDialogLabel"), Encoding.UTF8) + "&multimode=false&functionmarkup=";

                if (defaultFunction != null)
                {
                    string defaultValue = new FunctionRuntimeTreeNode(defaultFunction).Serialize().ToString();

                    btnDefaultValueFunctionMarkup.DefaultValue = defaultValue;
                    btnTestValueFunctionMarkup.DefaultValue = defaultValue;
                }
            }

            btnDelete.Attributes["isdisabled"] = CurrentlySelectedFieldId == Guid.Empty ? "true" : "false";

            if (nameChanged)
            {
                UpdateFieldsPanel();
            }

            _state.Parameters = this.CurrentFields.ToList();
            SessionStateManager.GetProvider(SessionStateProviderName).SetState(StateId, _state, DateTime.Now.AddDays(7.0));
        }


        private void InitializeDetailsSplitPanel()
        {
            UpdateDetailsSplitPanel(true);
            UpdatePositionFieldOptions();
        }


        private void UpdateDetailsSplitPanel(bool detailsSplitPanel )
        {
            if (DetailsSplitPanelPlaceHolder.Visible != detailsSplitPanel)
            {
                DetailsSplitPanelPlaceHolder.Visible = detailsSplitPanel;
            }
        }


        private void UpdatePositionFieldOptions()
        {
            var positionOptions = new Dictionary<int, string>();

            for (int i = 0; i < this.CurrentFields.Count; i++)
            {
                positionOptions.Add(i, (i + 1).ToString() + ".");
            }

            positionOptions.Add(-1, GetString("PositionLast"));

            this.PositionField.DataSource = positionOptions;
            this.PositionField.DataTextField = "Value";
            this.PositionField.DataValueField = "Key";
            this.PositionField.DataBind();
        }



        private void ResetWidgetSelector()
        {
            string widgetFunctionMarkup = "";

            WidgetFunctionProvider widgetFunctionProvider = StandardWidgetFunctions.GetDefaultWidgetFunctionProviderByType(this.CurrentlySelectedWidgetReturnType);
            if (widgetFunctionProvider != null)
            {
                widgetFunctionMarkup = widgetFunctionProvider.SerializedWidgetFunction.ToString(SaveOptions.DisableFormatting);
            } 

            btnWidgetFunctionMarkup.Value = widgetFunctionMarkup;

            if (widgetFunctionMarkup == "")
            {
                if (FunctionFacade.GetWidgetFunctionNamesByType(this.CurrentlySelectedWidgetReturnType).Any())
                {
                    Baloon(btnWidgetFunctionMarkup.ClientID, GetString("SpecifyWidgetTip"));
                }
            }
        }



        protected void PositionField_SelectedIndexChanged(object sender, EventArgs e)
        {
            FieldSettingsChanged(sender, e);
            UpdateFieldsPanel();
        }



        protected void TypeSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            FieldSettingsChanged(sender, e);

            ResetWidgetSelector();
        }



        protected void FieldDataList_ItemCommand(Object sender, EventArgs e)
        {
            RepeaterCommandEventArgs repeaterEventArgs = (RepeaterCommandEventArgs)e;
            Guid FieldId = new Guid(repeaterEventArgs.CommandArgument.ToString());

            if (ValidateSave() == true)
            {
                switch (repeaterEventArgs.CommandName)
                {
                    case "Select":
                        Field_Select(FieldId);
                        break;
                    default:
                        throw new Exception("unhandled item command name: " + repeaterEventArgs.CommandName);
                }
            }
            else
            {
                UpdateFieldsPanel();
            }
        }


        private void Field_Delete(Guid FieldId)
        {
            if (this.ViewState["Fields"] == null) throw new Exception("ViewState element 'Fields' does not exist");
            var fields = (List<ManagedParameterDefinition>)this.ViewState["Fields"];

            var field = fields.Single(f => f.Id == FieldId);

            fields.RemoveAll(f => f.Id == FieldId);

            if (this.CurrentlySelectedFieldId != Guid.Empty)
            {
                if (this.CurrentlySelectedFieldId == FieldId) this.CurrentlySelectedFieldId = Guid.Empty;
            }

            foreach (ManagedParameterDefinition laterField in this.CurrentFields.Where(f => f.Position > field.Position))
            {
                laterField.Position--;
            }


            UpdatePositionFieldOptions();

            this.PositionField.SelectedValue = "-1";
            this.NameField.Text = "";

            UpdateFieldsPanel();

            UpdateDetailsSplitPanel(false);
        }



        private void UpdateTypeList()
        {
            var typeList = 
                from type in this.TypeOptions
                orderby type.Name
                select new { HashCode = type.FullName.GetHashCode(), Label = type.GetShortLabel() };

            TypeSelector.DataSource = typeList;
            TypeSelector.DataTextField = "Label";
            TypeSelector.DataValueField = "HashCode";
            TypeSelector.DataBind();

        }



        private string SerializeType(Type t)
        {
            return TypeManager.SerializeType(t);
        }

        private void Field_Select(Guid FieldId)
        {
            if (ValidateSave() == true)
            {
                if (this.CurrentlySelectedFieldId != Guid.Empty)
                {
                    Field_Save();
                }

                InitializeDetailsSplitPanel();

                if (this.ViewState["Fields"] == null) throw new Exception("ViewState element 'Fields' does not exist");
                var Fields = (List<ManagedParameterDefinition>)this.ViewState["Fields"];

                var selectedField = Fields.Single(f => f.Id == FieldId);

                this.CurrentlySelectedFieldId = FieldId;

                this.NameField.Text = selectedField.Name;

                this.LabelField.Text = selectedField.Label;
                this.HelpField.Text = selectedField.HelpText;

                string typeName = selectedField.Type.FullName.GetHashCode().ToString();

                if (typeName != null && this.TypeSelector.Items.FindByValue(typeName) != null)
                {
                    this.TypeSelector.SelectedValue = typeName;
                }
                else
                {
                    this.TypeSelector.Items.Insert( 0, new ListItem( "UNKNOWN TYPE: " + selectedField.Type.Name ));
                }

                btnWidgetFunctionMarkup.Value = selectedField.WidgetFunctionMarkup;

                btnDefaultValueFunctionMarkup.Value = selectedField.DefaultValueFunctionMarkup;

                btnTestValueFunctionMarkup.Value = selectedField.TestValueFunctionMarkup;

                this.PositionField.SelectedValue = (selectedField.Position == this.CurrentFields.Count - 1 ? "-1" : selectedField.Position.ToString());
            }
        }



        protected Guid CurrentlySelectedFieldId
        {
            get
            {
                if (this.ViewState["editedFieldId"] == null)
                {
                    return Guid.Empty;
                }

                return new Guid(this.ViewState["editedFieldId"].ToString());
            }
            set
            {
                this.ViewState["editedFieldId"] = value;
            }
        }



        protected bool HasFields
        {
            get
            {
                return this.CurrentFields.Count > 0;
            }
        }


        private List<ManagedParameterDefinition> CurrentFields
        {
            get
            {
                if (this.ViewState["Fields"] == null) throw new Exception("ViewState element 'Fields' does not exist");
                return (List<ManagedParameterDefinition>)this.ViewState["Fields"];
            }

            set
            {
                this.ViewState["Fields"] = value;
            }
        }



        private Type GetInstanceTypeForReference( Type referencedType )
        {
            List<PropertyInfo> keyProperties = DataAttributeFacade.GetKeyProperties(referencedType);

            if (keyProperties.Count == 1)
            {
                return keyProperties[0].PropertyType;
            }
            else
            {
                // with multi key tyoes we go with a string
                return typeof(string);
            }
        }



        protected Type CurrentlySelectedWidgetReturnType
        {
            get
            {
                Type selectedType = this.CurrentlySelectedType;

                return selectedType;
            }
        }



        protected string CurrentlySelectedWidgetText
        {
            get
            {
                string widgetMarkup = btnWidgetFunctionMarkup.Value;

                if (widgetMarkup.IsNullOrEmpty())
                {
                    return GetString("NoWidgetSpecifiedLabel");
                }

                XElement functionElement = XElement.Parse(widgetMarkup);
                if (functionElement.Name.Namespace!=Namespaces.Function10)
                    functionElement = functionElement.Elements().First();

                try
                {
                    BaseFunctionRuntimeTreeNode widgetNode = (BaseFunctionRuntimeTreeNode)FunctionFacade.BuildTree(functionElement);
                    return widgetNode.GetName();
                }
                catch (Exception ex)
                {
                    Composite.Core.Log.LogError("FunctionParamter", ex);
                    Baloon(btnWidgetFunctionMarkup,"Error: "+ex.Message);
                    return GetString("NoWidgetSpecifiedLabel");
                }
            }
        }



        protected Type CurrentlySelectedType
        {
            get
            {
                Type selectedType = this.TypeOptions.Where(t => t.FullName.GetHashCode() == Int32.Parse(this.TypeSelector.SelectedValue)).First(); 

                return selectedType;
            }
        }



        private void ShowMessage(string targetFieldName, string p)
        {
            FieldMessage fm = new FieldMessage(targetFieldName, p);

        }



        public void btnAddNew_Click()
        {
            if (ValidateSave() == true)
            {
                if (this.CurrentlySelectedFieldId != Guid.Empty)
                    Field_Save();

                InitializeDetailsSplitPanel();

                this.CurrentlySelectedFieldId = Guid.NewGuid();
                this.NameField.Text = _defaultFieldNamePrefix;

                int i = 2;
                while (this.CurrentFields.Where(f => f.Name == this.NameField.Text).Count() > 0)
                {
                    this.NameField.Text = _defaultFieldNamePrefix + i++;
                }

                this.TypeSelector.SelectedValue = typeof(string).FullName.GetHashCode().ToString();
                btnDefaultValueFunctionMarkup.Value = "";
                btnTestValueFunctionMarkup.Value = "";
                this.LabelField.Text = "";
                this.HelpField.Text = "";
                this.PositionField.SelectedValue = "-1";

                ResetWidgetSelector();

                Field_Save();

                UpdatePositionFieldOptions();
                UpdateFieldsPanel();
                MakeClientDirty();
            }
        }



        private bool ValidateSave()
        {
            if (this.CurrentlySelectedFieldId == Guid.Empty) return true;

            if (this.NameField.Text.Contains(" ") == true)
            {
                Baloon(this.NameField, GetString("SpaceInNameError"));
                return false;
            }

            if (string.IsNullOrEmpty(this.NameField.Text) == true)
            {
                Baloon(this.NameField, GetString("NameEmptyError"));
                return false;
            }

            if (this.CurrentFields.Where(f=>f.Name == this.NameField.Text && f.Id != this.CurrentlySelectedFieldId ).Any() == true)
            {
                Baloon(this.NameField, GetString("NameAlreadyInUseError"));
                return false;
            }

            string toValidate = (this.NameField.Text.StartsWith("@") ? this.NameField.Text.Substring(1) : this.NameField.Text);
            string err;
            if (Composite.Data.DynamicTypes.NameValidation.TryValidateName(toValidate, out err)==false)
            {
                Baloon(this.NameField, err);
                return false;
            }

            return true;
        }



        private void Baloon(System.Web.UI.Control c, string message)
        {
            Baloon(c.ClientID.Replace("_", "$"), message);
        }

        private void Baloon(string fieldName, string message)
        {
            FieldMessage fm = new FieldMessage(fieldName, message);

            MessagesPlaceHolder.Controls.Add(fm);
        }

        private void UpdateFieldsPanel()
        {
            this.FieldListRepeater.DataSource = this.ViewState["Fields"];
            this.FieldListRepeater.DataBind();
        }



        private void Field_Save()
        {
            if (CurrentlySelectedFieldId == Guid.Empty)
            {
                return;
            }

            if (this.CurrentFields.Count(f => f.Id == this.CurrentlySelectedFieldId) == 0)
            {
                ManagedParameterDefinition newField = new ManagedParameterDefinition {
                   Id = this.CurrentlySelectedFieldId, 
                   Name = this.NameField.Text, 
                   Type = this.CurrentlySelectedType
                };
                newField.Position = this.CurrentFields.Count;
                this.CurrentFields.Add(newField);
            }

            if (FieldNameSyntaxValid(this.NameField.Text) == false)
            {
                ShowMessage(this.NameField.ClientID, GetString("FieldNameSyntaxInvalid"));
                return;
            }

            if (this.CurrentFields.Count<ManagedParameterDefinition>(f => f.Name.ToLower() == this.NameField.Text.ToLower() && f.Id != this.CurrentlySelectedFieldId) > 0)
            {
                ShowMessage(this.NameField.ClientID, GetString("CannotSave"));
                return;
            }

            var field = this.CurrentFields.Single(f => f.Id == this.CurrentlySelectedFieldId);

            if (field.Name != this.NameField.Text)
            {
                nameChanged = true;
            }

            field.Name = this.NameField.Text;
            field.Type = this.CurrentlySelectedType;

            bool generateLabel = (this.LabelField.Text == "" && this.NameField.Text.StartsWith(_defaultFieldNamePrefix) == false);
            string label = (generateLabel ? this.NameField.Text : this.LabelField.Text);

            field.Label = label;
            field.HelpText = this.HelpField.Text;

            if (!btnWidgetFunctionMarkup.Value.IsNullOrEmpty())
            {
                XElement functionElement = XElement.Parse(btnWidgetFunctionMarkup.Value);
                if (functionElement.Name.Namespace != Namespaces.Function10)
                    functionElement = functionElement.Elements().First();

                field.WidgetFunctionMarkup = functionElement.ToString(SaveOptions.DisableFormatting);
            }
            else
            {
                field.WidgetFunctionMarkup = "";
            }


            if (!btnDefaultValueFunctionMarkup.Value.IsNullOrEmpty())
            {
                XElement functionElement = XElement.Parse(btnDefaultValueFunctionMarkup.Value);
                if (functionElement.Name.Namespace != Namespaces.Function10)
                    functionElement = functionElement.Elements().First();

                field.DefaultValueFunctionMarkup = functionElement.ToString(SaveOptions.DisableFormatting);
            }
            else
            {
                field.DefaultValueFunctionMarkup = null;
            }


            if (!btnTestValueFunctionMarkup.Value.IsNullOrEmpty())
            {
                XElement functionElement = XElement.Parse(btnTestValueFunctionMarkup.Value);
                if (functionElement.Name.Namespace != Namespaces.Function10)
                    functionElement = functionElement.Elements().First();

                field.TestValueFunctionMarkup = functionElement.ToString(SaveOptions.DisableFormatting);
            }
            else
            {
                field.TestValueFunctionMarkup = null;
            }


            int newPosition = int.Parse(this.PositionField.SelectedValue);
            if (newPosition == -1) newPosition = this.CurrentFields.Count - 1;

            if (field.Position != newPosition)
            {
                this.CurrentFields.Remove(field);

                foreach (ManagedParameterDefinition laterField in this.CurrentFields.Where(f => f.Position > field.Position))
                {
                    laterField.Position--;
                }

                foreach (ManagedParameterDefinition laterField in this.CurrentFields.Where(f => f.Position >= newPosition))
                {
                    laterField.Position++;
                }

                field.Position = newPosition;
                this.CurrentFields.Insert(newPosition, field);
            }

        }


        public void btnDelete_Click()
        {
            if (CurrentlySelectedFieldId == Guid.Empty)
            {
                return;
            }

            var fields = (List<ManagedParameterDefinition>)this.ViewState["Fields"];
            List<Guid> fieldIDs = fields.Select(field => field.Id).ToList();

            int currentFieldOffset = fieldIDs.IndexOf(CurrentlySelectedFieldId);

            Field_Delete(this.CurrentlySelectedFieldId);
            MakeClientDirty();

            if (currentFieldOffset < fieldIDs.Count - 1)
            {
                CurrentlySelectedFieldId = Guid.Empty;
                Field_Select(fieldIDs[currentFieldOffset + 1]);
            }
        }


        private void MakeClientDirty()
        {
            MakeDirtyEventPlaceHolder.Visible = true;
        }

        private bool FieldNameSyntaxValid(string name)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(name.Trim()))
            {
                return false;
            }

            return true;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="uiControlMarkup">The visual content of the form. All namespaces that controls and functions belong to must be declared.</param>
        /// <param name="bindingDeclarationMarkup">Bining declarations - a list of elements like &lt;binding name="..." type="..." optional="false" xmlns="http://www.composite.net/ns/management/bindingforms/1.0" /></param>
        /// <returns></returns>
        private FormDefinition BuildFormDefinition(XNode bindingsDeclarationMarkup, XNode uiControlMarkup, Dictionary<string, object> bindings)
        {
            XNamespace placeholderSpace = "#internal";
            XNamespace stdControlLibSpace = Namespaces.BindingFormsStdUiControls10;

            string formXml =
            #region XML for form
 @"<?xml version='1.0' encoding='utf-8' ?>
<cms:formdefinition
  xmlns:internal='" + placeholderSpace + @"'
  xmlns:cms='" + Namespaces.BindingForms10 + @"'>

  <internal:bindingsDeclarationPlaceholder />

  <cms:layout>
    <!--FieldGroup xmlns='" + stdControlLibSpace + @"'-->
      <internal:uiControlPlaceholder />
    <!--/FieldGroup-->
  </cms:layout>
  
</cms:formdefinition>";
            #endregion

            var formMarkup = XDocument.Parse(formXml);

            XElement bindingDeclarationPlaceholder = formMarkup.Descendants(placeholderSpace + "bindingsDeclarationPlaceholder").First();

            bindingDeclarationPlaceholder.ReplaceWith(bindingsDeclarationMarkup);

            XElement uiControlPlaceholder = formMarkup.Descendants(placeholderSpace + "uiControlPlaceholder").First();
            uiControlPlaceholder.ReplaceWith(uiControlMarkup);
            return new FormDefinition(formMarkup.CreateReader(), bindings);
        }


        // one of the "post backing" fields has been changed on the client
        protected void FieldSettingsChanged(object sender, EventArgs e)
        {
            if (this.CurrentlySelectedFieldId != Guid.Empty)
            {
                if (ValidateSave() == true)
                {
                    Field_Save();
                }
            }
        }



        // Saving data to the form dictionary...
        protected void BindStateToProperties()
        {
            // TODO: to be used
            if (this.CurrentlySelectedFieldId != Guid.Empty)
            {
                if (ValidateSave() == true)
                {
                    Field_Save();
                }
            }



            foreach (var field in this.CurrentFields)
            {
                if (string.IsNullOrEmpty(field.Label) == true)
                {
                    field.Label = field.Name;
                }
            }


            this.Parameters = this.CurrentFields;
        }



        // First time we run - we are attached to a parent System.Web.Control 
        protected void InitializeViewState()
        {
            List<ManagedParameterDefinition> fields = new List<ManagedParameterDefinition>();
            if (this.Parameters != null) fields.AddRange(this.Parameters);

            // ensure positioning is in place
            int position = 0;
            foreach (ManagedParameterDefinition field in fields.OrderBy(f => f.Position))
            {
                field.Position = position++;
            }

            this.ViewState.Add("Fields", fields);
            this.ViewState.Add("editedPatameterId", null);

            UpdateFieldsPanel();
        }

        protected string EventTarget
        {
            get
            {
                return IsPostBack ? Request.Form["__EVENTTARGET"] : string.Empty;
            }
        }
    }
}