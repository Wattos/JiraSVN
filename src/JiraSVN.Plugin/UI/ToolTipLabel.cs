#region Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Drawing;
using System.Windows.Forms;

namespace JiraSVN.Plugin.UI
{
	internal class ToolTipLabel : Label
	{
		public ToolTipLabel()
		{
			BorderStyle = BorderStyle.FixedSingle;
			BackColor = SystemColors.Info;
			ForeColor = SystemColors.InfoText;
			AutoSize = false;
			Width = DisplayWidth = 400;
			Height = 150;
			Visible = false;
		}

		public int DisplayWidth { get; set; }

		public override string Text
		{
			get { return base.Text; }
			set
			{
				if (String.IsNullOrEmpty(value) || Parent == null)
				{
					base.Text = String.Empty;
					Visible = false;
					if (Parent != null) 
						Parent.MouseLeave -= Parent_MouseLeave;
				}
				else
				{
					base.Text = value;
					Parent.Controls.SetChildIndex(this, 0);
					Parent.MouseLeave += Parent_MouseLeave;

					SizeF size = Size;
					try
					{
						using (Graphics g = Graphics.FromHwnd(Handle))
							size = g.MeasureString(Text, Font, DisplayWidth);
					}
					catch
					{
					}

					Width = 4 + Margin.Horizontal + (int)size.Width;
					Height = 4 + Margin.Vertical + (int)size.Height;
				}
			}
		}

		private void Parent_MouseLeave(object sender, EventArgs e)
		{
			Visible = false;
			Parent.MouseLeave -= Parent_MouseLeave;
		}
	}
}