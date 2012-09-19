//    XSDDiagram - A XML Schema Definition file viewer
//    Copyright (C) 2006-2011  Regis COSNIER
//
//    This program is free software; you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation; either version 2 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program; if not, write to the Free Software
//    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

// To generate the XMLSchema.cs file:
// > xsd.exe XMLSchema.xsd /classes /l:cs /n:XMLSchema /order

using XSDDiagram.Rendering;
using System.Xml.Schema;
using System.Diagnostics;

namespace XSDDiagram
{
	public partial class MainForm : Form
	{
        private DiagramPrinter _diagramPrinter;

        private Diagram diagram = new Diagram();
        private Schema schema = new Schema();
        private Dictionary<string, TabPage> hashtableTabPageByFilename = new Dictionary<string, TabPage>();
		private string originalTitle = "";
		private DiagramItem contextualMenuPointedElement = null;
        //private string currentLoadedSchemaFilename = "";

        private TextBox textBoxAnnotation;
        private WebBrowser webBrowserDocumentation;
		private bool webBrowserSupported = true;

		public MainForm()
		{
			InitializeComponent();

			this.toolsToolStripMenuItem.Visible = !Options.IsRunningOnMono;

			this.originalTitle = Text;

			this.toolStripComboBoxSchemaElement.Sorted = true;
			this.toolStripComboBoxSchemaElement.Items.Add("");

			this.diagram.RequestAnyElement += new Diagram.RequestAnyElementEventHandler(diagram_RequestAnyElement);
			this.panelDiagram.DiagramControl.ContextMenuStrip = this.contextMenuStripDiagram;
			this.panelDiagram.DiagramControl.MouseWheel += new MouseEventHandler(DiagramControl_MouseWheel);
			this.panelDiagram.DiagramControl.MouseClick += new MouseEventHandler(DiagramControl_MouseClick);
			this.panelDiagram.DiagramControl.MouseHover += new EventHandler(DiagramControl_MouseHover);
			this.panelDiagram.DiagramControl.MouseMove += new MouseEventHandler(DiagramControl_MouseMove);
			this.panelDiagram.VirtualSize = new Size(0, 0);
			this.panelDiagram.DiagramControl.Paint += new PaintEventHandler(DiagramControl_Paint);

			if (Options.IsRunningOnMono)
			{
				try
				{
					new WebBrowser().Navigate("about:blank");
				}
				catch
				{
					webBrowserSupported = false;
				}
			}
		}

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                    components = null;
                }

                if (_diagramPrinter != null)
                {
                    _diagramPrinter.Dispose();
                    _diagramPrinter = null;
                }
            }

            base.Dispose(disposing);
        }

		private void MainForm_Load(object sender, EventArgs e)
		{
			this.toolStripComboBoxZoom.SelectedIndex = 8;
			this.toolStripComboBoxAlignement.SelectedIndex = 1;

			if (!string.IsNullOrEmpty(Options.InputFile))
			{
				LoadSchema(Options.InputFile);
				foreach (var rootElement in Options.RootElements)
				{
					foreach (var element in schema.Elements)
					{
						if (element.Name == rootElement)
						{
							diagram.Add(element.Tag, element.NameSpace);
						}
					}
				}
				for (int i = 0; i < Options.ExpandLevel; i++)
				{
					diagram.ExpandOneLevel();
				}
				UpdateDiagram();
			}
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "xsd files (*.xsd)|*.xsd|All files (*.*)|*.*";
			openFileDialog.FilterIndex = 1;
			openFileDialog.RestoreDirectory = true;
			if (openFileDialog.ShowDialog() == DialogResult.OK)
				LoadSchema(openFileDialog.FileName);
		}

		private void MainForm_DragDrop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				LoadSchema(files[0]);
			}
		}

		private void MainForm_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effect = DragDropEffects.Move;
			else
				e.Effect = DragDropEffects.None;
		}

		private void saveDiagramToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = "SVG files (*.svg)|*.svg" + (Options.IsRunningOnMono ? "" : "|EMF files (*.emf)|*.emf") + "|PNG files (*.png)|*.png|JPG files (*.jpg)|*.jpg|All files (*.*)|*.*";
			saveFileDialog.FilterIndex = 1;
			saveFileDialog.RestoreDirectory = true;
			if (saveFileDialog.ShowDialog() == DialogResult.OK)
			{
                string outputFilename = saveFileDialog.FileName;
				try
				{
                    DiagramExporter exporter = new DiagramExporter(diagram);
                    Graphics g1 = this.panelDiagram.DiagramControl.CreateGraphics();
                    exporter.Export(outputFilename, g1, new DiagramAlertHandler(SaveAlert));
                    g1.Dispose();
				}
                catch (System.ArgumentException ex)
                {
                    MessageBox.Show("You have reach the system limit.\r\nPlease remove some element from the diagram to make it smaller.");
                    System.Diagnostics.Trace.WriteLine(ex.ToString());
                }
                catch (System.Runtime.InteropServices.ExternalException ex)
                {
                    MessageBox.Show("You have reach the system limit.\r\nPlease remove some element from the diagram to make it smaller.");
                    System.Diagnostics.Trace.WriteLine(ex.ToString());
                }
                catch (Exception ex)
				{
                    MessageBox.Show(ex.Message);
					System.Diagnostics.Trace.WriteLine(ex.ToString());
				}
			}
		}

        bool SaveAlert(string title, string message)
        {
            return MessageBox.Show(this, message, title, MessageBoxButtons.YesNo) == DialogResult.Yes;
        }

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
		{
			AboutForm aboutForm = new AboutForm();
			aboutForm.ShowDialog(this);
		}

		private void toolStripComboBoxSchemaElement_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (this.toolStripComboBoxSchemaElement.SelectedItem != null)
			{
				XSDObject xsdObject = this.toolStripComboBoxSchemaElement.SelectedItem as XSDObject;
				if (xsdObject != null)
					SelectSchemaElement(xsdObject);
			}
		}

		private void toolStripButtonAddToDiagram_Click(object sender, EventArgs e)
		{
			if (this.toolStripComboBoxSchemaElement.SelectedItem != null)
			{
				XSDObject xsdObject = this.toolStripComboBoxSchemaElement.SelectedItem as XSDObject;
				if (xsdObject != null)
					this.diagram.Add(xsdObject.Tag, xsdObject.NameSpace);
				UpdateDiagram();
			}
		}

		private void toolStripButtonAddAllToDiagram_Click(object sender, EventArgs e)
		{
			foreach (XSDObject xsdObject in this.schema.ElementsByName.Values)
				if (xsdObject != null)
					this.diagram.Add(xsdObject.Tag, xsdObject.NameSpace);
			UpdateDiagram();
		}

		void DiagramControl_Paint(object sender, PaintEventArgs e)
		{
			Point virtualPoint = this.panelDiagram.VirtualPoint;
			e.Graphics.TranslateTransform(-(float)virtualPoint.X, -(float)virtualPoint.Y);
			e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
			e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            DiagramGdiRenderer.Draw(diagram, e.Graphics, 
                new Rectangle(virtualPoint, this.panelDiagram.DiagramControl.ClientRectangle.Size));
		}

		private void UpdateDiagram()
		{
			if (this.diagram.RootElements.Count != 0)
			{
				Graphics g = this.panelDiagram.DiagramControl.CreateGraphics();
				this.diagram.Layout(g);
				g.Dispose();
				Size bbSize = this.diagram.BoundingBox.Size + this.diagram.Padding + this.diagram.Padding;
				this.panelDiagram.VirtualSize = new Size((int)(bbSize.Width * this.diagram.Scale), (int)(bbSize.Height * this.diagram.Scale));
			}
			else
				this.panelDiagram.VirtualSize = new Size(0, 0);
		}

		private void UpdateTitle(string filename)
		{
			if (filename.Length > 0)
				Text = this.originalTitle + " - " + filename;
			else
				Text = this.originalTitle;
		}

		private void LoadSchema(string schemaFilename)
		{
			Cursor = Cursors.WaitCursor;

			UpdateTitle(schemaFilename);

			this.diagram.Clear();
			this.panelDiagram.VirtualSize = new Size(0, 0);
			this.panelDiagram.VirtualPoint = new Point(0, 0);
			this.panelDiagram.Clear();
			this.hashtableTabPageByFilename.Clear();
			this.listViewElements.Items.Clear();
			this.toolStripComboBoxSchemaElement.Items.Clear();
			this.toolStripComboBoxSchemaElement.Items.Add("");
			this.propertyGridSchemaObject.SelectedObject = null;

			while (this.tabControlView.TabCount > 1)
				this.tabControlView.TabPages.RemoveAt(1);


            schema.LoadSchema(schemaFilename);

            foreach (XSDObject xsdObject in schema.Elements)
            {
                this.listViewElements.Items.Add(new ListViewItem(new string[] { xsdObject.Name, xsdObject.Type, xsdObject.NameSpace })).Tag = xsdObject;
                this.toolStripComboBoxSchemaElement.Items.Add(xsdObject);
            }

			Cursor = Cursors.Default;

			if (this.schema.LoadError.Count > 0)
			{
				ErrorReportForm errorReportForm = new ErrorReportForm();
				errorReportForm.Errors = this.schema.LoadError;
				errorReportForm.ShowDialog(this);
			}

			this.diagram.ElementsByName = this.schema.ElementsByName;
			if (this.schema.FirstElement != null)
				this.toolStripComboBoxSchemaElement.SelectedItem = this.schema.FirstElement;
			else
				this.toolStripComboBoxSchemaElement.SelectedIndex = 0;

			tabControlView_Selected(null, null);

			this.tabControlView.SuspendLayout();
			foreach (string filename in this.schema.XsdFilenames)
			{
                string fullPath = filename;
                Control browser = null;
				if(webBrowserSupported)
                    browser = new WebBrowser();
                else
                    browser = new System.Windows.Forms.TextBox() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both };
                browser.Dock = DockStyle.Fill;
                browser.TabIndex = 0;
                try
                {
                    new Uri(filename);
                }
                catch
                {
                    fullPath = Path.GetFullPath(filename);
                }
				TabPage tabPage = new TabPage(Path.GetFileNameWithoutExtension(filename));
				tabPage.Tag = fullPath;
				tabPage.ToolTipText = fullPath;
                tabPage.Controls.Add(browser);
				tabPage.UseVisualStyleBackColor = true;

				this.tabControlView.TabPages.Add(tabPage);
				this.hashtableTabPageByFilename[filename] = tabPage;

			}
			this.tabControlView.ResumeLayout();

            //currentLoadedSchemaFilename = schemaFilename;
		}

		void DiagramControl_MouseClick(object sender, MouseEventArgs e)
		{
			Point location = e.Location;
			location.Offset(this.panelDiagram.VirtualPoint);

			DiagramItem resultElement;
			DiagramHitTestRegion resultRegion;
			this.diagram.HitTest(location, out resultElement, out resultRegion);
			if (resultRegion != DiagramHitTestRegion.None)
			{
				if (resultRegion == DiagramHitTestRegion.ChildExpandButton)
				{
					if (resultElement.HasChildElements)
					{
						if (resultElement.ChildElements.Count == 0)
						{
							this.diagram.ExpandChildren(resultElement);
							resultElement.ShowChildElements = true;
						}
						else
							resultElement.ShowChildElements ^= true;

						UpdateDiagram();
						this.panelDiagram.ScrollTo(this.diagram.ScalePoint(resultElement.Location), true);
					}
				}
				else if (resultRegion == DiagramHitTestRegion.Element)
				{
					if ((ModifierKeys & (Keys.Control | Keys.Shift)) == (Keys.Control | Keys.Shift)) // For Debug
					{
						this.toolStripComboBoxSchemaElement.SelectedItem = "";
						this.propertyGridSchemaObject.SelectedObject = resultElement;
					}
					else
						SelectDiagramElement(resultElement);
				}
				else
					SelectDiagramElement(null);
			}
		}

		private void SelectDiagramElement(DiagramItem element)
		{
			this.textBoxElementPath.Text = "";
			if (element == null)
			{
				this.toolStripComboBoxSchemaElement.SelectedItem = "";
				this.propertyGridSchemaObject.SelectedObject = null;
				this.listViewAttributes.Items.Clear();
				return;
			}

			if (element is DiagramItem)
			{
                //if (this.schema.ElementsByName[element.FullName] != null)
                //	this.toolStripComboBoxSchemaElement.SelectedItem = this.schema.ElementsByName[element.FullName];
                XSDObject xsdObject;
                if (this.schema.ElementsByName.TryGetValue(element.FullName, out xsdObject) && xsdObject != null)
                    this.toolStripComboBoxSchemaElement.SelectedItem = xsdObject;
				else
					this.toolStripComboBoxSchemaElement.SelectedItem = null;

				SelectSchemaElement(element);

				string path = '/' + element.Name;
				DiagramItem parentElement = element.Parent;
				while (parentElement != null)
				{
					if (parentElement.ItemType == DiagramItemType.element && !string.IsNullOrEmpty(parentElement.Name))
						path = '/' + parentElement.Name + path;
					parentElement = parentElement.Parent;
				}
				this.textBoxElementPath.Text = path;
			}
			else
			{
				this.toolStripComboBoxSchemaElement.SelectedItem = "";
				SelectSchemaElement(element);
			}
		}

		private void SelectSchemaElement(XSDObject xsdObject)
		{
			SelectSchemaElement(xsdObject.Tag, xsdObject.NameSpace);
		}

		private void SelectSchemaElement(DiagramItem diagramBase)
		{
			SelectSchemaElement(diagramBase.TabSchema, diagramBase.NameSpace);
		}

		private void SelectSchemaElement(XMLSchema.openAttrs openAttrs, string nameSpace)
		{
			this.propertyGridSchemaObject.SelectedObject = openAttrs;
			ShowDocumentation(null);

			XMLSchema.annotated annotated = openAttrs as XMLSchema.annotated;
			if (annotated != null)
			{
				// Element documentation
				if (annotated.annotation != null)
					ShowDocumentation(annotated.annotation);

				// Show the enumeration
				ShowEnumerate(annotated);

				// Attributes enumeration
				List<XSDAttribute> listAttributes = new List<XSDAttribute>();
				if (annotated is XMLSchema.element)
				{
					XMLSchema.element element = annotated as XMLSchema.element;

					if (element.Item is XMLSchema.complexType)
					{
						XMLSchema.complexType complexType = element.Item as XMLSchema.complexType;
						listAttributes.AddRange(ShowAttributes(complexType, nameSpace));
					}
					else if (element.type != null)
					{
                        //XSDObject xsdObject = this.schema.ElementsByName[QualifiedNameToFullName("type", element.type)] as XSDObject;
                        //if (xsdObject != null)
                        XSDObject xsdObject;
                        if (this.schema.ElementsByName.TryGetValue(QualifiedNameToFullName("type", element.type), out xsdObject) && xsdObject != null)
						{
							XMLSchema.annotated annotatedElement = xsdObject.Tag as XMLSchema.annotated;
							if (annotatedElement is XMLSchema.complexType)
							{
								XMLSchema.complexType complexType = annotatedElement as XMLSchema.complexType;
								listAttributes.AddRange(ShowAttributes(complexType, nameSpace));
							}
							else
							{
							}
						}
						else
						{
						}
					}
					else
					{
					}
				}
				else if (annotated is XMLSchema.complexType)
				{
					XMLSchema.complexType complexType = annotated as XMLSchema.complexType;
					listAttributes.AddRange(ShowAttributes(complexType, nameSpace));
				}
                //RC++ Original code
                //else
                //{
                //}

                //this.listViewAttributes.Items.Clear();
                //foreach (XSDAttribute attribute in listAttributes)
                //    this.listViewAttributes.Items.Add(new ListViewItem(new string[] { attribute.Name, attribute.Type, attribute.Use, attribute.DefaultValue })).Tag = attribute;
                //RC--

                //Adrian++
                //This part i modify
                else if (annotated is XMLSchema.simpleType)
                {
                    XMLSchema.attribute attr = new XMLSchema.attribute();
                    XMLSchema.localSimpleType def = new XMLSchema.localSimpleType();
                    def.Item = (annotated as XMLSchema.simpleType).Item;
                    attr.simpleType = def;
                    string type = "";
                    if (def.Item is XMLSchema.restriction) type = (def.Item as XMLSchema.restriction).@base.Name;
                    XSDAttribute XSDattr = new XSDAttribute("filename", (annotated as XMLSchema.simpleType).name, "namespace", type, false, "", "", attr);
                    listAttributes.Add(XSDattr);

                }
                //This part i modify
                this.listViewAttributes.Items.Clear();
                listAttributes.Reverse();
                foreach (XSDAttribute attribute in listAttributes)
                {
                    string s = "";
                    //dgis fix github issue 2 (attribute.Tag == null ???)
                    if (attribute.Tag != null && attribute.Tag.simpleType != null && attribute.Tag.simpleType.Item is XMLSchema.restriction)
                    {
                        XMLSchema.restriction r = attribute.Tag.simpleType.Item as XMLSchema.restriction;
                        if (r.Items != null)
                        {
                            for (int i = 0; i < r.Items.Length; i++)
                            {
                                s += r.ItemsElementName[i].ToString() + "(" + r.Items[i].id + " " + r.Items[i].value + ");";
                            }
                        }
                    }

                    this.listViewAttributes.Items.Add(new ListViewItem(new string[] { attribute.Name, attribute.Type, attribute.Use, attribute.DefaultValue, s })).Tag = attribute;
                }
                //Adrian--
			}
		}

		private List<XSDAttribute> ShowAttributes(XMLSchema.complexType complexType, string nameSpace)
		{
			List<XSDAttribute> listAttributes = new List<XSDAttribute>();
			ParseComplexTypeAttributes(nameSpace, listAttributes, complexType, false);
			return listAttributes;
		}

		private void ParseComplexTypeAttributes(string nameSpace, List<XSDAttribute> listAttributes, XMLSchema.complexType complexType, bool isRestriction)
		{
			if (complexType.ItemsElementName != null)
			{
				for (int i = 0; i < complexType.ItemsElementName.Length; i++)
				{
					switch (complexType.ItemsElementName[i])
					{
						case XMLSchema.ItemsChoiceType4.attribute:
							{
								XMLSchema.attribute attribute = complexType.Items[i] as XMLSchema.attribute;
								ParseAttribute(nameSpace, listAttributes, attribute, false);
							}
							break;
						case XMLSchema.ItemsChoiceType4.attributeGroup:
							{
								XMLSchema.attributeGroup attributeGroup = complexType.Items[i] as XMLSchema.attributeGroup;
								ParseAttributeGroup(nameSpace, listAttributes, attributeGroup, false);
							}
							break;
						case XMLSchema.ItemsChoiceType4.anyAttribute:
							XMLSchema.wildcard wildcard = complexType.Items[i] as XMLSchema.wildcard;
							XSDAttribute xsdAttribute = new XSDAttribute("", "*", wildcard.@namespace, "", false, null, null, null);
							listAttributes.Add(xsdAttribute);
							break;
						case XMLSchema.ItemsChoiceType4.simpleContent:
						case XMLSchema.ItemsChoiceType4.complexContent:
							XMLSchema.annotated annotatedContent = null;
							if (complexType.Items[i] is XMLSchema.complexContent)
							{
								XMLSchema.complexContent complexContent = complexType.Items[i] as XMLSchema.complexContent;
								annotatedContent = complexContent.Item;
							}
							else if (complexType.Items[i] is XMLSchema.simpleContent)
							{
								XMLSchema.simpleContent simpleContent = complexType.Items[i] as XMLSchema.simpleContent;
								annotatedContent = simpleContent.Item;
							}
							if (annotatedContent is XMLSchema.extensionType)
							{
								XMLSchema.extensionType extensionType = annotatedContent as XMLSchema.extensionType;
                                //XSDObject xsdExtensionType = this.schema.ElementsByName[QualifiedNameToFullName("type", extensionType.@base)] as XSDObject;
                                //if (xsdExtensionType != null)
                                XSDObject xsdExtensionType;
                                if (this.schema.ElementsByName.TryGetValue(QualifiedNameToFullName("type", extensionType.@base), out xsdExtensionType) && xsdExtensionType != null)
                                {
									XMLSchema.annotated annotatedExtension = xsdExtensionType.Tag as XMLSchema.annotated;
									if (annotatedExtension != null)
									{
										if (annotatedExtension is XMLSchema.complexType)
											ParseComplexTypeAttributes(extensionType.@base.Namespace, listAttributes, annotatedExtension as XMLSchema.complexType, false);
									}
								}
								if (extensionType.Items != null)
								{
									foreach (XMLSchema.annotated annotated in extensionType.Items)
									{
										if (annotated is XMLSchema.attribute)
										{
											ParseAttribute(nameSpace, listAttributes, annotated as XMLSchema.attribute, false);
										}
										else if (annotated is XMLSchema.attributeGroup)
										{
											ParseAttributeGroup(nameSpace, listAttributes, annotated as XMLSchema.attributeGroup, false);
										}
									}
								}
							}
							else if (annotatedContent is XMLSchema.restrictionType)
							{
								XMLSchema.restrictionType restrictionType = annotatedContent as XMLSchema.restrictionType;
                                //XSDObject xsdRestrictionType = this.schema.ElementsByName[QualifiedNameToFullName("type", restrictionType.@base)] as XSDObject;
                                //if (xsdRestrictionType != null)
                                XSDObject xsdRestrictionType;
                                if (this.schema.ElementsByName.TryGetValue(QualifiedNameToFullName("type", restrictionType.@base), out xsdRestrictionType) && xsdRestrictionType != null)
                                {
									XMLSchema.annotated annotatedRestriction = xsdRestrictionType.Tag as XMLSchema.annotated;
									if (annotatedRestriction != null)
									{
										if (annotatedRestriction is XMLSchema.complexType)
											ParseComplexTypeAttributes(restrictionType.@base.Namespace, listAttributes, annotatedRestriction as XMLSchema.complexType, false);
									}
								}
								if (restrictionType.Items1 != null)
								{
									foreach (XMLSchema.annotated annotated in restrictionType.Items1)
									{
										if (annotated is XMLSchema.attribute)
										{
											ParseAttribute(nameSpace, listAttributes, annotated as XMLSchema.attribute, true);
										}
										else if (annotated is XMLSchema.attributeGroup)
										{
											ParseAttributeGroup(nameSpace, listAttributes, annotated as XMLSchema.attributeGroup, true);
										}
									}
								}
							}
							break;
					}
				}
			}
			else
			{
			}
		}

		private void ParseAttribute(string nameSpace, List<XSDAttribute> listAttributes, XMLSchema.attribute attribute, bool isRestriction)
		{
			bool isReference = false;
			string filename = "";
			string name = attribute.name;
			string type = "";
			if (attribute.@ref != null)
			{
                object o = null;
                this.schema.AttributesByName.TryGetValue(QualifiedNameToFullName("attribute", attribute.@ref), out o);
                if (o is XSDAttribute)
				{
					XSDAttribute xsdAttributeInstance = o as XSDAttribute;
					ParseAttribute(nameSpace, listAttributes, xsdAttributeInstance.Tag, isRestriction);
					return;
				}
				else // Reference not found!
				{
					type = QualifiedNameToAttributeTypeName(attribute.@ref);
					name = attribute.@ref.Name;
					nameSpace = attribute.@ref.Namespace;
					isReference = true;
				}
			}
			else if (attribute.type != null)
			{
				type = QualifiedNameToAttributeTypeName(attribute.type);
				nameSpace = attribute.type.Namespace;
			}
			else if (attribute.simpleType != null)
			{
				XMLSchema.simpleType simpleType = attribute.simpleType as XMLSchema.simpleType;
				if (simpleType.Item is XMLSchema.restriction)
				{
					XMLSchema.restriction restriction = simpleType.Item as XMLSchema.restriction;
					type = QualifiedNameToAttributeTypeName(restriction.@base);
					nameSpace = restriction.@base.Namespace;
				}
				else if (simpleType.Item is XMLSchema.list)
				{
					XMLSchema.list list = simpleType.Item as XMLSchema.list;
					type = QualifiedNameToAttributeTypeName(list.itemType);
					nameSpace = list.itemType.Namespace;
				}
				else
				{
				}
			}
			else
			{

			}
			if (string.IsNullOrEmpty(attribute.name) && string.IsNullOrEmpty(name))
			{
			}
			if (isRestriction)
			{
				if (attribute.use == XMLSchema.attributeUse.prohibited)
				{
					foreach (XSDAttribute xsdAttribute in listAttributes)
					{
						if (xsdAttribute.Name == name)
						{
							//listAttributes.Remove(xsdAttribute);
							xsdAttribute.Use = attribute.use.ToString();
							break;
						}
					}
				}
			}
			else
			{
				XSDAttribute xsdAttribute = new XSDAttribute(filename, name, nameSpace, type, isReference, attribute.@default, attribute.use.ToString(), attribute);
				listAttributes.Insert(0, xsdAttribute);
			}
		}

		private void ParseAttributeGroup(string nameSpace, List<XSDAttribute> listAttributes, XMLSchema.attributeGroup attributeGroup, bool isRestriction)
		{
			if (attributeGroup is XMLSchema.attributeGroupRef && attributeGroup.@ref != null)
			{
                object o = null;
                this.schema.AttributesByName.TryGetValue(QualifiedNameToFullName("attributeGroup", attributeGroup.@ref), out o);
                if (o is XSDAttributeGroup)
				{
					XSDAttributeGroup xsdAttributeGroup = o as XSDAttributeGroup;
					XMLSchema.attributeGroup attributeGroupInstance = xsdAttributeGroup.Tag;

					foreach (XMLSchema.annotated annotated in attributeGroupInstance.Items)
					{
						if (annotated is XMLSchema.attribute)
						{
							ParseAttribute(nameSpace, listAttributes, annotated as XMLSchema.attribute, isRestriction);
						}
						else if (annotated is XMLSchema.attributeGroup)
						{
							ParseAttributeGroup(nameSpace, listAttributes, annotated as XMLSchema.attributeGroup, isRestriction);
						}
					}
				}
			}
			else
			{

			}
		}

		private static string QualifiedNameToFullName(string type, System.Xml.XmlQualifiedName xmlQualifiedName)
		{
			return xmlQualifiedName.Namespace + ':' + type + ':' + xmlQualifiedName.Name;
		}

		private static string QualifiedNameToAttributeTypeName(System.Xml.XmlQualifiedName xmlQualifiedName)
		{
			return xmlQualifiedName.Name + "  : " + xmlQualifiedName.Namespace;
		}

		private void ShowEnumerate(XMLSchema.attribute attribute)
		{
			this.listViewEnumerate.Items.Clear();
			if (attribute != null)
			{
				if (attribute.type != null)
				{
                    //XSDObject xsdObject = this.schema.ElementsByName[QualifiedNameToFullName("type", attribute.type)] as XSDObject;
                    //if (xsdObject != null)
                    XSDObject xsdObject;
                    if (this.schema.ElementsByName.TryGetValue(QualifiedNameToFullName("type", attribute.type), out xsdObject) && xsdObject != null)
                    {
						XMLSchema.annotated annotatedElement = xsdObject.Tag as XMLSchema.annotated;
						if (annotatedElement is XMLSchema.simpleType)
							ShowEnumerate(annotatedElement as XMLSchema.simpleType);
					}
				}
				else if (attribute.simpleType != null)
				{
					ShowEnumerate(attribute.simpleType);
				}
			}
		}

		private void ShowEnumerate(XMLSchema.annotated annotated)
		{
			this.listViewEnumerate.Items.Clear();

			if (annotated != null)
			{
				XMLSchema.element element = annotated as XMLSchema.element;
				if (element != null && element.type != null)
				{
                    //XSDObject xsdObject = this.schema.ElementsByName[QualifiedNameToFullName("type", element.type)] as XSDObject;
                    //if (xsdObject != null)
                    XSDObject xsdObject;
                    if (this.schema.ElementsByName.TryGetValue(QualifiedNameToFullName("type", element.type), out xsdObject) && xsdObject != null)
                    {
						XMLSchema.annotated annotatedElement = xsdObject.Tag as XMLSchema.annotated;
						if (annotatedElement is XMLSchema.simpleType)
							ShowEnumerate(annotatedElement as XMLSchema.simpleType);
					}
				}
			}
		}

		private void ShowEnumerate(XMLSchema.simpleType simpleType)
		{
			if (simpleType != null)
			{
				if (simpleType.Item != null)
				{
					XMLSchema.restriction restriction = simpleType.Item as XMLSchema.restriction;
					if (restriction != null && restriction.ItemsElementName != null)
					{
						for (int i = 0; i < restriction.ItemsElementName.Length; i++)
						{
							if (restriction.ItemsElementName[i] == XMLSchema.ItemsChoiceType.enumeration)
							{
								XMLSchema.facet facet = restriction.Items[i] as XMLSchema.facet;
								if (facet != null)
									this.listViewEnumerate.Items.Add(facet.value);
							}
						}

						if (this.listViewEnumerate.Items.Count != 0)
							this.listViewEnumerate.Columns[0].Width = -1;
					}
				}
			}
		}


		private void ShowDocumentation(XMLSchema.annotation annotation)
		{
            if (this.textBoxAnnotation == null)
            {
                // 
                // webBrowserDocumentation
                // 
                if(webBrowserSupported)
                {
                    this.webBrowserDocumentation = new System.Windows.Forms.WebBrowser();
                    this.webBrowserDocumentation.Dock = System.Windows.Forms.DockStyle.Fill;
                    this.webBrowserDocumentation.Location = new System.Drawing.Point(0, 0);
                    this.webBrowserDocumentation.MinimumSize = new System.Drawing.Size(20, 20);
                    this.webBrowserDocumentation.Name = "webBrowserDocumentation";
                    this.webBrowserDocumentation.Size = new System.Drawing.Size(214, 117);
                    this.webBrowserDocumentation.TabIndex = 1;
                    this.splitContainerDiagramElement.Panel2.Controls.Add(this.webBrowserDocumentation);
                }
                else
                    this.webBrowserDocumentation = null;
                // 
                // textBoxAnnotation
                // 
                this.textBoxAnnotation = new System.Windows.Forms.TextBox();
                this.textBoxAnnotation.Dock = System.Windows.Forms.DockStyle.Fill;
                this.textBoxAnnotation.Location = new System.Drawing.Point(0, 0);
                this.textBoxAnnotation.Multiline = true;
                this.textBoxAnnotation.Name = "textBoxAnnotation";
                this.textBoxAnnotation.ReadOnly = true;
                this.textBoxAnnotation.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
                this.textBoxAnnotation.Size = new System.Drawing.Size(214, 117);
                this.textBoxAnnotation.TabIndex = 0;
                this.splitContainerDiagramElement.Panel2.Controls.Add(this.textBoxAnnotation);
            }
			if (annotation == null)
			{
				this.textBoxAnnotation.Text = "";
				this.textBoxAnnotation.Visible = true;
                if (this.webBrowserDocumentation != null)
    				this.webBrowserDocumentation.Visible = false;

				return;
			}

			foreach (object o in annotation.Items)
			{
				if (o is XMLSchema.documentation)
				{
					XMLSchema.documentation documentation = o as XMLSchema.documentation;
					if (documentation.Any != null && documentation.Any.Length > 0)
					{
						string text = documentation.Any[0].Value;
						text = text.Replace("\n", " ");
						text = text.Replace("\t", " ");
						text = text.Replace("\r", "");
						text = Regex.Replace(text, " +", " ");
						text = text.Trim();

						//text = text.Replace(, " ");

						//text = text.Trim('\n', '\t', '\r', ' ');
						//string[] textLines = text.Split(new char[] { '\n' });
						//for (int i = 0; i < textLines.Length; i++)
						//    textLines[i] = textLines[i].Trim('\n', '\t', '\r', ' ');
						//text = string.Join("\r\n", textLines);
						this.textBoxAnnotation.Text = text;
						this.textBoxAnnotation.Visible = true;
                        if (this.webBrowserDocumentation != null)
                            this.webBrowserDocumentation.Visible = false;
					}
					else if (documentation.source != null)
					{
                        if (this.webBrowserDocumentation != null)
                        {
                            this.textBoxAnnotation.Visible = false;
                            this.webBrowserDocumentation.Visible = true;
                            this.webBrowserDocumentation.Navigate(documentation.source);
                        }
                        else
                        {
                            this.textBoxAnnotation.Text = documentation.source;
                            this.textBoxAnnotation.Visible = true;
                        }
					}
					break;
				}
			}
		}

		private void listViewAttributes_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (this.listViewAttributes.SelectedItems.Count > 0)
			{
				XSDAttribute xsdAttribute = this.listViewAttributes.SelectedItems[0].Tag as XSDAttribute;
				XMLSchema.attribute attribute = xsdAttribute.Tag;
				if (attribute != null && attribute.annotation != null)
					ShowDocumentation(attribute.annotation);
				else
					ShowDocumentation(null);
				ShowEnumerate(attribute);
			}
		}

		private void toolStripComboBoxZoom_SelectedIndexChanged(object sender, EventArgs e)
		{
			try
			{
				string zoomString = this.toolStripComboBoxZoom.SelectedItem as string;
				zoomString = zoomString.Replace("%", "");

				float zoom = (float)int.Parse(zoomString) / 100.0f;
				if (zoom >= 0.10 && zoom <= 10)
				{
					//Point virtualCenter = this.panelDiagram.VirtualPoint;
					//virtualCenter.Offset(this.panelDiagram.DiagramControl.Width / 2, this.panelDiagram.DiagramControl.Height / 2);
					//Size oldSize = this.panelDiagram.VirtualSize;
					//this.diagram.Scale = zoom;
					//UpdateDiagram();
					//Size newSize = this.panelDiagram.VirtualSize;
					//virtualCenter.X = (int)((float)newSize.Width / (float)oldSize.Width * (float)virtualCenter.X);
					//virtualCenter.Y = (int)((float)newSize.Height / (float)oldSize.Height * (float)virtualCenter.Y);
					//if (virtualCenter.X > this.diagram.BoundingBox.Right)
					//    virtualCenter.X = this.diagram.BoundingBox.Right;
					//if (virtualCenter.Y > this.diagram.BoundingBox.Bottom)
					//    virtualCenter.Y = this.diagram.BoundingBox.Bottom;
					//this.panelDiagram.ScrollTo(virtualCenter, true);

					Point virtualCenter = this.panelDiagram.VirtualPoint;
					virtualCenter.Offset(this.panelDiagram.DiagramControl.Width / 2, this.panelDiagram.DiagramControl.Height / 2);
					Size oldSize = this.panelDiagram.VirtualSize;
					this.diagram.Scale = zoom;
					UpdateDiagram();
					Size newSize = this.panelDiagram.VirtualSize;
					Point newVirtualCenter = new Point();
					newVirtualCenter.X = (int)((float)newSize.Width / (float)oldSize.Width * (float)virtualCenter.X);
					newVirtualCenter.Y = (int)((float)newSize.Height / (float)oldSize.Height * (float)virtualCenter.Y);
					if (newVirtualCenter.X > this.diagram.BoundingBox.Right)
						newVirtualCenter.X = this.diagram.BoundingBox.Right;
					if (newVirtualCenter.Y > this.diagram.BoundingBox.Bottom)
						newVirtualCenter.Y = this.diagram.BoundingBox.Bottom;
					this.panelDiagram.ScrollTo(newVirtualCenter, true);
				}
			}
			catch { }
		}

		private void toolStripComboBoxZoom_TextChanged(object sender, EventArgs e)
		{
			//try
			//{
			//    string zoomString = this.toolStripComboBoxZoom.SelectedItem as string;
			//    zoomString = zoomString.Replace("%", "");

			//    float zoom = (float)int.Parse(zoomString) / 100.0f;
			//    if (zoom >= 0.10 && zoom <= 10)
			//    {
			//        this.diagram.Scale = zoom;
			//        UpdateDiagram();
			//    }
			//}
			//catch { }
		}

		void DiagramControl_MouseWheel(object sender, MouseEventArgs e)
		{
			if ((ModifierKeys & Keys.Control) == Keys.Control)
			{
				if (e.Delta > 0)
				{
					if (this.toolStripComboBoxZoom.SelectedIndex < this.toolStripComboBoxZoom.Items.Count - 1)
						this.toolStripComboBoxZoom.SelectedIndex++;
				}
				else
				{
					if (this.toolStripComboBoxZoom.SelectedIndex > 0)
						this.toolStripComboBoxZoom.SelectedIndex--;
				}
			}
		}

		private void pageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
                if (_diagramPrinter == null)
                {
                    _diagramPrinter = new DiagramPrinter();
                }
                _diagramPrinter.Diagram = diagram;

                _diagramPrinter.PageSetup();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void printPreviewToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
                if (_diagramPrinter == null)
                {
                    _diagramPrinter = new DiagramPrinter();
                }
                _diagramPrinter.Diagram = diagram;

                _diagramPrinter.PrintPreview();
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void printToolStripMenuItem_Click(object sender, EventArgs e)
		{
			try
			{
                if (_diagramPrinter == null)
                {
                    _diagramPrinter = new DiagramPrinter();
                }
                _diagramPrinter.Diagram = diagram;

                _diagramPrinter.Print(true, Options.IsRunningOnMono);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		private void toolStripButtonTogglePanel_Click(object sender, EventArgs e)
		{
			this.splitContainerMain.Panel2Collapsed = !this.toolStripButtonTogglePanel.Checked;
		}

		private void contextMenuStripDiagram_Opened(object sender, EventArgs e)
		{
			this.gotoXSDFileToolStripMenuItem.Enabled = false;
			this.removeFromDiagramToolStripMenuItem.Enabled = false;

			Point contextualMenuMousePosition = this.panelDiagram.DiagramControl.PointToClient(MousePosition);
			contextualMenuMousePosition.Offset(this.panelDiagram.VirtualPoint);
			DiagramItem resultElement;
			DiagramHitTestRegion resultRegion;
			this.diagram.HitTest(contextualMenuMousePosition, out resultElement, out resultRegion);
			if (resultRegion != DiagramHitTestRegion.None)
			{
				if (resultRegion == DiagramHitTestRegion.Element) // && resultElement.Parent == null)
				{
					this.contextualMenuPointedElement = resultElement;
                    this.gotoXSDFileToolStripMenuItem.Enabled = this.schema.ElementsByName.ContainsKey(this.contextualMenuPointedElement.FullName);
					this.removeFromDiagramToolStripMenuItem.Enabled = true;
				}
			}
		}

		private void gotoXSDFileToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (this.contextualMenuPointedElement != null)
			{
				//XSDObject xsdObject = this.schema.ElementsByName[this.contextualMenuPointedElement.FullName] as XSDObject;
                //if (xsdObject != null)
				XSDObject xsdObject;
                if (this.schema.ElementsByName.TryGetValue(this.contextualMenuPointedElement.FullName, out xsdObject) && xsdObject != null)
				{
                    TabPage tabPage = null;
                    if (this.hashtableTabPageByFilename.TryGetValue(xsdObject.Filename, out tabPage) && tabPage != null)
						this.tabControlView.SelectedTab = tabPage;
				}
			}
			this.contextualMenuPointedElement = null;
		}

		private void removeFromDiagramToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (this.contextualMenuPointedElement != null)
			{
				DiagramItem parentDiagram = this.contextualMenuPointedElement.Parent;
				this.diagram.Remove(this.contextualMenuPointedElement);
				UpdateDiagram();
				if (parentDiagram != null)
					this.panelDiagram.ScrollTo(this.diagram.ScalePoint(parentDiagram.Location), true);
				else
					this.panelDiagram.ScrollTo(new Point(0, 0));
			}
			this.contextualMenuPointedElement = null;
		}

		private void tabControlView_Selected(object sender, TabControlEventArgs e)
		{
			if (tabControlView.SelectedTab.Tag != null)
			{
				WebBrowser webBrowser = tabControlView.SelectedTab.Controls[0] as WebBrowser;
                if (webBrowser != null)
                {
                    string url = tabControlView.SelectedTab.Tag as string;
                    //if (webBrowser.Url == null || webBrowser.Url != new Uri(url))
                    if (webBrowser.Document == null)
                        webBrowser.Navigate(url);
                    webBrowser.Select();
                }
                else
                {
                    TextBox textBrowser = tabControlView.SelectedTab.Controls[0] as TextBox;
                    if (textBrowser != null)
                    {
                        string url = tabControlView.SelectedTab.Tag as string;
                        if (string.IsNullOrEmpty(textBrowser.Text))
                        {
                            try
                            {
                                //HttpWebRequest webRequestObject = (HttpWebRequest)WebRequest.Create(url);
                                ////WebRequestObject.UserAgent = ".NET Framework/2.0";
                                ////WebRequestObject.Referer = "http://www.example.com/";
                                //WebResponse response = webRequestObject.GetResponse();
                                //Stream webStream = response.GetResponseStream();
                                //StreamReader reader = new StreamReader(webStream);
                                //textBrowser.Text = reader.ReadToEnd();
                                //reader.Close();
                                //webStream.Close();
                                //response.Close();

                                WebClient client = new WebClient();
                                client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                                Stream data = client.OpenRead(url);
                                StreamReader reader = new StreamReader(data);
                                textBrowser.Text = reader.ReadToEnd().Replace("\r\n", "\n").Replace("\n", "\r\n");
                                data.Close();
                                reader.Close();
                            }
                            catch (Exception ex)
                            {
                                textBrowser.Text = ex.Message;
                            }
                        }
                        textBrowser.Select();
                    }
                }
			}
		}

		private void tabControlView_Click(object sender, EventArgs e)
		{
			if (tabControlView.SelectedTab.Tag != null)
			{
                Control webBrowser = tabControlView.SelectedTab.Controls[0] as Control;
				if (webBrowser != null)
					webBrowser.Select();
			}
		}

		private void tabControlView_Enter(object sender, EventArgs e)
		{
            if (tabControlView.SelectedTab != null && tabControlView.SelectedTab.Tag != null)
			{
                Control webBrowser = tabControlView.SelectedTab.Controls[0] as Control;
				if (webBrowser != null)
					webBrowser.Focus();
			}
			else
				this.panelDiagram.Focus();
		}

		private void registerToolStripMenuItem_Click(object sender, EventArgs e)
		{
			FileShellExtension.Register(Microsoft.Win32.Registry.GetValue("HKEY_CLASSES_ROOT\\.xsd", null, "xsdfile") as string, "XSDDiagram", "XSD Diagram", string.Format("\"{0}\" \"%L\"", Application.ExecutablePath));
		}

		private void unregisterToolStripMenuItem_Click(object sender, EventArgs e)
		{
			FileShellExtension.Unregister(Microsoft.Win32.Registry.GetValue("HKEY_CLASSES_ROOT\\.xsd", null, "xsdfile") as string, "XSDDiagram");
		}

		private void toolStripButtonShowReferenceBoundingBox_Click(object sender, EventArgs e)
		{
			this.diagram.ShowBoundingBox = this.toolStripButtonShowReferenceBoundingBox.Checked;
			UpdateDiagram();
		}

		private void toolStripComboBoxAlignement_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (this.toolStripComboBoxAlignement.SelectedItem as string)
			{
				case "Top": this.diagram.Alignement = DiagramAlignement.Near; break;
				case "Center": this.diagram.Alignement = DiagramAlignement.Center; break;
				case "Bottom": this.diagram.Alignement = DiagramAlignement.Far; break;
			}
			UpdateDiagram();
		}

		void diagram_RequestAnyElement(DiagramItem diagramElement, out XMLSchema.element element, out string nameSpace)
		{
			element = null;
			nameSpace = "";

			//ElementsForm elementsForm = new ElementsForm();
			//elementsForm.Location = MousePosition; //diagramElement.Location //MousePosition;
			//elementsForm.ListBoxElements.Items.Clear();
			//elementsForm.ListBoxElements.Items.Insert(0, "(Cancel)");
			//foreach (XSDObject xsdObject in this.schema.ElementsByName.Values)
			//    if (xsdObject != null && xsdObject.Type == "element")
			//        elementsForm.ListBoxElements.Items.Add(xsdObject);
			//if (elementsForm.ShowDialog(this.diagramControl) == DialogResult.OK && (elementsForm.ListBoxElements.SelectedItem as XSDObject) != null)
			//{
			//    XSDObject xsdObject = elementsForm.ListBoxElements.SelectedItem as XSDObject;
			//    element = xsdObject.Tag as XMLSchema.element;
			//    nameSpace = xsdObject.NameSpace;
			//}
		}

		private void listViewElement_Click(object sender, EventArgs e)
		{
			if (this.listViewElements.SelectedItems.Count > 0)
				SelectSchemaElement(this.listViewElements.SelectedItems[0].Tag as XSDObject);
		}

		private void listViewElement_DoubleClick(object sender, EventArgs e)
		{
			if (this.listViewElements.SelectedItems.Count > 0)
			{
				foreach (ListViewItem lvi in this.listViewElements.SelectedItems)
				{
					XSDObject xsdObject = lvi.Tag as XSDObject;
					this.diagram.Add(xsdObject.Tag as XMLSchema.openAttrs, xsdObject.NameSpace);
					//switch (xsdObject.Type)
					//{
					//    case "element":
					//        this.diagram.AddElement(xsdObject.Tag as XMLSchema.element, xsdObject.NameSpace);
					//        break;
					//    case "group":
					//        this.diagram.AddCompositors(xsdObject.Tag as XMLSchema.group, xsdObject.NameSpace);
					//        break;
					//    case "complexType":
					//        this.diagram.AddComplexType(xsdObject.Tag as XMLSchema.complexType, xsdObject.NameSpace);
					//        break;
					//    case "simpleType":
					//        this.diagram.Add(xsdObject.Tag as XMLSchema.simpleType, xsdObject.NameSpace);
					//        break;
					//}
				}
				UpdateDiagram();
			}
		}

		private void expandOneLevelToolStripMenuItem_Click(object sender, EventArgs e)
		{
			this.diagram.ExpandOneLevel();
			UpdateDiagram();
		}

		// Implements the manual sorting of items by columns.
		class ListViewItemComparer : IComparer
		{
			private int column;
			private ListView listView;
			public ListViewItemComparer(int column, ListView listView)
			{
				this.column = column;
				this.listView = listView;

				switch (this.listView.Sorting)
				{
					case SortOrder.None: this.listView.Sorting = SortOrder.Ascending; break;
					case SortOrder.Ascending: this.listView.Sorting = SortOrder.Descending; break;
					case SortOrder.Descending: this.listView.Sorting = SortOrder.Ascending; break;
				}
			}
			public int Compare(object x, object y)
			{
				int result = 0;
				if (this.listView.Sorting == SortOrder.Ascending)
					result = String.Compare(((ListViewItem)x).SubItems[this.column].Text, ((ListViewItem)y).SubItems[column].Text);
				if (this.listView.Sorting == SortOrder.Descending)
					result = -String.Compare(((ListViewItem)x).SubItems[this.column].Text, ((ListViewItem)y).SubItems[column].Text);

				return result;
			}
		}

		private void listViewElement_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			this.listViewElements.ListViewItemSorter = new ListViewItemComparer(e.Column, this.listViewElements);
		}

		private void listViewAttributes_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			this.listViewAttributes.ListViewItemSorter = new ListViewItemComparer(e.Column, this.listViewAttributes);
		}

		private void toolStripButtonRemoveAllFromDiagram_Click(object sender, EventArgs e)
		{
			this.diagram.RemoveAll();
			UpdateDiagram();
			this.panelDiagram.VirtualPoint = new Point(0, 0);
			this.panelDiagram.Clear();
		}

		private void listView_AfterLabelEdit(object sender, LabelEditEventArgs e)
		{
			e.CancelEdit = true;
		}

		private void nextTabToolStripMenuItem_Click(object sender, EventArgs e)
		{
			int index = this.tabControlView.SelectedIndex;
			++index;
			this.tabControlView.SelectedIndex = index % this.tabControlView.TabCount;
		}

		private void previousTabToolStripMenuItem_Click(object sender, EventArgs e)
		{
			int index = this.tabControlView.SelectedIndex;
			--index;
			if (index < 0) index = this.tabControlView.TabCount - 1;
			this.tabControlView.SelectedIndex = index;
		}

		private void ListViewToString(ListView listView, bool selectedLineOnly)
		{
			string result = "";
			if (selectedLineOnly)
			{
				if (listView.SelectedItems.Count > 0)
				{
					foreach (ColumnHeader columnHeader in listView.Columns)
					{
						if (columnHeader.Index > 0) result += "\t";
						result += listView.SelectedItems[0].SubItems[columnHeader.Index].Text;
					}
				}
			}
			else
			{
				foreach (ListViewItem lvi in listView.Items)
				{
					foreach (ColumnHeader columnHeader in listView.Columns)
					{
						if (columnHeader.Index > 0) result += "\t";
						result += lvi.SubItems[columnHeader.Index].Text;
					}
					result += "\r\n";
				}
			}
			if (result.Length > 0)
				Clipboard.SetText(result);
		}

		private void toolStripMenuItemAttributesCopyLine_Click(object sender, EventArgs e)
		{
			ListViewToString(this.listViewAttributes, true);
		}

		private void toolStripMenuItemAttributesCopyList_Click(object sender, EventArgs e)
		{
			ListViewToString(this.listViewAttributes, false);
		}

		private void contextMenuStripAttributes_Opening(object sender, CancelEventArgs e)
		{
			this.toolStripMenuItemAttributesCopyLine.Enabled = (this.listViewAttributes.SelectedItems.Count == 1);
			this.toolStripMenuItemAttributesCopyList.Enabled = (this.listViewAttributes.Items.Count > 0);
		}

		private void toolStripMenuItemEnumerateCopyLine_Click(object sender, EventArgs e)
		{
			ListViewToString(this.listViewEnumerate, true);
		}

		private void toolStripMenuItemEnumerateCopyList_Click(object sender, EventArgs e)
		{
			ListViewToString(this.listViewEnumerate, false);
		}

		private void contextMenuStripEnumerate_Opening(object sender, CancelEventArgs e)
		{
			this.toolStripMenuItemEnumerateCopyLine.Enabled = (this.listViewEnumerate.SelectedItems.Count == 1);
			this.toolStripMenuItemEnumerateCopyList.Enabled = (this.listViewEnumerate.Items.Count > 0);
		}

		private void toolStripMenuItemElementsCopyLine_Click(object sender, EventArgs e)
		{
			ListViewToString(this.listViewElements, true);
		}

		private void toolStripMenuItemElementsCopyList_Click(object sender, EventArgs e)
		{
			ListViewToString(this.listViewElements, false);
		}

		private void contextMenuStripElements_Opening(object sender, CancelEventArgs e)
		{
			this.toolStripMenuItemElementsCopyLine.Enabled = (this.listViewElements.SelectedItems.Count == 1);
			this.toolStripMenuItemElementsCopyList.Enabled = (this.listViewElements.Items.Count > 0);
		}

		private void listViewElements_ItemDrag(object sender, ItemDragEventArgs e)
		{
			listViewElements.DoDragDrop(e.Item, DragDropEffects.Copy);
		}

		private void panelDiagram_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(typeof(ListViewItem)))
			{
				ListViewItem lvi = e.Data.GetData(typeof(ListViewItem)) as ListViewItem;
				if (lvi != null)
				{
					XSDObject xsdObject = lvi.Tag as XSDObject;
					switch (xsdObject.Type)
					{
						case "element":
						case "group":
						case "complexType":
							e.Effect = DragDropEffects.Copy;
							break;
					}
				}
			}
			else if (e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effect = DragDropEffects.Move;
		}

		private void panelDiagram_DragDrop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(typeof(ListViewItem)))
			{
				ListViewItem lvi = e.Data.GetData(typeof(ListViewItem)) as ListViewItem;
				if (lvi != null)
				{
					listViewElement_DoubleClick(sender, e);
				}
			}
			else if (e.Data.GetDataPresent(DataFormats.FileDrop))
				MainForm_DragDrop(sender, e);
		}

		void DiagramControl_MouseMove(object sender, MouseEventArgs e)
		{
			//toolTip.Show("Coucou", panelDiagram.DiagramControl, 200);
		}

		void DiagramControl_MouseHover(object sender, EventArgs e)
		{
		}

		//private void toolTip_Popup(object sender, PopupEventArgs e)
		//{
		//    //toolTip.SetToolTip(e.AssociatedControl, "AAAAAAAAAA");
		//}

		private void toolTip_Draw(object sender, DrawToolTipEventArgs e)
		{
			Point diagramMousePosition = e.AssociatedControl.PointToClient(MousePosition);
			string text = string.Format("AAAA {0} {1}\nA Que\n\nCoucou", diagramMousePosition.X, diagramMousePosition.Y);

			Size textSize = TextRenderer.MeasureText(text, e.Font);
			Rectangle newBound = new Rectangle(e.Bounds.X + 20, e.Bounds.Y - 20, textSize.Width + 10, textSize.Height + 10);

			DrawToolTipEventArgs newArgs = new DrawToolTipEventArgs(e.Graphics,
				e.AssociatedWindow, e.AssociatedControl, newBound, text,
				this.BackColor, this.ForeColor, e.Font);
			newArgs.DrawBackground();
			newArgs.DrawBorder();
			newArgs.DrawText(TextFormatFlags.TextBoxControl);

			//e.DrawBackground();
			//e.DrawBorder();
			//using (StringFormat sf = new StringFormat())
			//{
			//    sf.Alignment = StringAlignment.Center;
			//    sf.LineAlignment = StringAlignment.Center;
			//    sf.HotkeyPrefix = System.Drawing.Text.HotkeyPrefix.None;
			//    sf.FormatFlags = StringFormatFlags.NoWrap;
			//    using (Font f = new Font("Tahoma", 9))
			//    {
			//        e.Graphics.DrawString(text, f,
			//            SystemBrushes.ActiveCaptionText, e.Bounds, sf);
			//    }
			//}
			//e.DrawText();
		}

        private void validateXMLFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "xml files (*.xml)|*.xml|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 2;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    Cursor = Cursors.WaitCursor;

                    validationErrorMessages.Clear();

                    StreamReader streamReader = new StreamReader(openFileDialog.FileName);
                    string xmlSource = streamReader.ReadToEnd();
                    streamReader.Close();

					//XmlDocument x = new XmlDocument();
					//x.LoadXml(xmlSource);

                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.CloseInput = true;
                    settings.ValidationType = ValidationType.Schema;
                    settings.ProhibitDtd = false;
					settings.XmlResolver = null;
                    settings.ValidationEventHandler += new ValidationEventHandler(ValidationHandler);
                    settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings |
                                                XmlSchemaValidationFlags.ProcessIdentityConstraints |
                                                XmlSchemaValidationFlags.ProcessInlineSchema |
                                                XmlSchemaValidationFlags.ProcessSchemaLocation
												; //| XmlSchemaValidationFlags.AllowXmlAttributes;
                    //settings.Schemas.Add("http://www.collada.org/2005/11/COLLADASchema", currentLoadedSchemaFilename);
                    //settings.Schemas.Add(null, currentLoadedSchemaFilename); // = sc;
                    List<string> schemas = new List<string>(schema.XsdFilenames);
                    schemas.Reverse();
					foreach (string schemaFilename in schemas)
					{
						try
						{
							settings.Schemas.Add(null, schemaFilename);
						}
						catch (Exception ex)
						{
							validationErrorMessages.Add(string.Format("Error while parsing {0}, Message: {1}",
								schemaFilename, ex.Message));
						}
					}

                    StringReader r = new StringReader(xmlSource);
                    using (XmlReader validatingReader = XmlReader.Create(r, settings))
                    {
                        while (validatingReader.Read()) { /* just loop through document */ }
                    }

                    Cursor = Cursors.Default;

                    ErrorReportForm errorReportForm = new ErrorReportForm();
                    errorReportForm.Errors = validationErrorMessages;
                    errorReportForm.ShowDialog(this);
                }
                catch (Exception ex)
                {
					Cursor = Cursors.Default;

					validationErrorMessages.Add(string.Format("Error while validating {0}, Message: {1}",
						openFileDialog.FileName, ex.Message));
					//MessageBox.Show("Cannot validate: " + ex.Message);
					ErrorReportForm errorReportForm = new ErrorReportForm();
					errorReportForm.Errors = validationErrorMessages;
					errorReportForm.ShowDialog(this);
                }
				Cursor = Cursors.Default;

				if (validationErrorMessages.Count == 0)
					MessageBox.Show("No issue found");
			}
        }

        static List<string> validationErrorMessages = new List<string>();

        public static void ValidationHandler(object sender, ValidationEventArgs e)
        {
            //if (e.Severity == XmlSeverityType.Error || e.Severity == XmlSeverityType.Warning)
            validationErrorMessages.Add(string.Format("{4}: [{3}] Line: {0}, Position: {1} \"{2}\"",
                e.Exception.LineNumber, e.Exception.LinePosition, e.Exception.Message, validationErrorMessages.Count, e.Severity));
        }

        private void toolsToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            validateXMLFileToolStripMenuItem.Enabled = (schema != null && schema.XsdFilenames.Count != 0);
        }

		//void DiagramControl_MouseMove(object sender, MouseEventArgs e)
		//{
		//System.Diagnostics.Trace.WriteLine("toolTipDiagramElement_Popup");
		//Point contextualMenuMousePosition = this.panelDiagram.DiagramControl.PointToClient(MousePosition);
		//contextualMenuMousePosition.Offset(this.panelDiagram.VirtualPoint);
		//DiagramBase resultElement;
		//DiagramBase.HitTestRegion resultRegion;
		//this.diagram.HitTest(contextualMenuMousePosition, out resultElement, out resultRegion);
		//if (resultRegion != DiagramBase.HitTestRegion.None)
		//{
		//    if (resultRegion == DiagramBase.HitTestRegion.Element) // && resultElement.Parent == null)
		//    {
		//        //this.contextualMenuPointedElement = resultElement;
		//        //toolTipDiagramElement.SetToolTip(this.panelDiagram.DiagramControl, "coucou");
		//        e.Cancel = true;
		//        toolTipElement.Show("Coucou", this);
		//    }
		//}
		//}
	}
}