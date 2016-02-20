﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace LogExpert.Dialogs
{
	public partial class TimeSpreadingControl : UserControl
	{
		private int EDGE_OFFSET = (int)Win32.GetSystemMetrics(Win32.SM_CYVSCROLL);
		private static readonly NLog.ILogger _logger = NLog.LogManager.GetCurrentClassLogger();

		private TimeSpreadCalculator timeSpreadCalc;
		private Bitmap bmp = new Bitmap(1, 1);
		private Object monitor = new Object();
		private ToolTip toolTip;
		private int lastMouseY = 0;
		private int rectHeight = 1;
		private int displayHeight = 1;

		public bool ReverseAlpha { get; set; }

		internal TimeSpreadCalculator TimeSpreadCalc
		{
			get
			{
				return timeSpreadCalc;
			}
			set
			{
				//timeSpreadCalc.CalcDone -= timeSpreadCalc_CalcDone;
				timeSpreadCalc = value;
				timeSpreadCalc.CalcDone += timeSpreadCalc_CalcDone;
				timeSpreadCalc.StartCalc += timeSpreadCalc_StartCalc;
			}
		}

		public TimeSpreadingControl()
		{
			InitializeComponent();
			this.toolTip = new ToolTip();
			this.toolTip.InitialDelay = 0;
			this.toolTip.ReshowDelay = 0;
			this.toolTip.ShowAlways = true;
			this.DoubleBuffered = false;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			lock (this.monitor)
			{
				if (DesignMode)
				{
					Brush bgBrush = new SolidBrush(Color.FromKnownColor(KnownColor.LightSkyBlue));
					Rectangle rect = ClientRectangle;
					rect.Inflate(0, -EDGE_OFFSET);
					e.Graphics.FillRectangle(bgBrush, rect);
					bgBrush.Dispose();
				}
				else
				{
					e.Graphics.DrawImage(this.bmp, 0, EDGE_OFFSET);
				}
			}
		}

		private void timeSpreadCalc_CalcDone()
		{
			_logger.logDebug("timeSpreadCalc_CalcDone()");
			lock (this.monitor)
			{
				this.Invalidate();
				Rectangle rect = this.ClientRectangle;
				rect.Size = new Size(rect.Width, rect.Height - EDGE_OFFSET * 3);
				if (rect.Height < 1)
					return;
				this.bmp = new Bitmap(rect.Width, rect.Height);
				Graphics gfx = Graphics.FromImage(bmp);
				Brush bgBrush = new SolidBrush(this.BackColor);
				gfx.FillRectangle(bgBrush, rect);
				bgBrush.Dispose();

				List<SpreadEntry> list = TimeSpreadCalc.DiffList;
				int step;
				if (list.Count >= this.displayHeight)
				{
					step = (int)Math.Round((double)list.Count / (double)this.displayHeight);
					this.rectHeight = 1;
				}
				else
				{
					step = 1;
					this.rectHeight = (int)Math.Round((double)this.displayHeight / (double)list.Count);
				}
				Rectangle fillRect = new Rectangle(0, 0, rect.Width, this.rectHeight);

				lock (list)
				{
					for (int i = 0; i < list.Count; i += step)
					{
						SpreadEntry entry = list[i];
						int color = ReverseAlpha ? entry.Value : 255 - entry.Value;
						if (color > 255)
						{
							color = 255;
						}
						if (color < 0)
						{
							color = 0;
						}
						Brush brush = new SolidBrush(Color.FromArgb(color, this.ForeColor));
						//Brush brush = new SolidBrush(Color.FromArgb(color, color, color, color));
						gfx.FillRectangle(brush, fillRect);
						brush.Dispose();
						fillRect.Offset(0, this.rectHeight);
					}
				}
			}
			this.BeginInvoke(new MethodInvoker(Refresh));
		}

		private void timeSpreadCalc_StartCalc()
		{
			lock (this.monitor)
			{
				this.Invalidate();
				Rectangle rect = this.ClientRectangle;
				rect.Size = new Size(rect.Width, rect.Height - EDGE_OFFSET * 3);
				if (rect.Height < 1)
					return;
				//this.bmp = new Bitmap(rect.Width, rect.Height);
				Graphics gfx = Graphics.FromImage(this.bmp);
				Brush bgBrush = new SolidBrush(this.BackColor);
				Brush fgBrush = new SolidBrush(this.ForeColor);
				//gfx.FillRectangle(bgBrush, rect);
				StringFormat format = new StringFormat(
													   StringFormatFlags.DirectionVertical |
													   StringFormatFlags.NoWrap);
				format.LineAlignment = StringAlignment.Center;
				format.Alignment = StringAlignment.Center;
				RectangleF rectf = new RectangleF(rect.Left, rect.Top, rect.Width, rect.Height);
				gfx.DrawString("Calculating time spread view...", this.Font, fgBrush, rectf, format);
				bgBrush.Dispose();
				fgBrush.Dispose();
			}
			this.BeginInvoke(new MethodInvoker(Refresh));
		}

		private void TimeSpreadingControl_SizeChanged(object sender, EventArgs e)
		{
			if (this.TimeSpreadCalc != null)
			{
				this.displayHeight = this.ClientRectangle.Height - EDGE_OFFSET * 3;
				this.TimeSpreadCalc.DisplayHeight = this.displayHeight;
			}
		}

		private void TimeSpreadingControl_MouseDown(object sender, MouseEventArgs e)
		{
		}

		private void TimeSpreadingControl_MouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				SpreadEntry entry = GetEntryForMouse(e);
				if (entry == null)
					return;
				OnLineSelected(new SelectLineEventArgs(entry.LineNum));
			}
		}

		private void TimeSpreadingControl_MouseEnter(object sender, EventArgs e)
		{
			this.toolTip.Active = true;
		}

		private void TimeSpreadingControl_MouseLeave(object sender, EventArgs e)
		{
			this.toolTip.Active = false;
		}

		private void TimeSpreadingControl_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.Y == this.lastMouseY)
				return;

			if (e.Button == MouseButtons.Right)
			{
				DragContrast(e);
				return;
			}

			SpreadEntry entry = GetEntryForMouse(e);
			if (entry == null)
				return;
			this.lastMouseY = e.Y;
			string dts = entry.Timestamp.ToString("dd.MM.yyyy HH:mm:ss");
			this.toolTip.SetToolTip(this, "Line " + (entry.LineNum + 1) + "\n" + dts);
		}

		private SpreadEntry GetEntryForMouse(MouseEventArgs e)
		{
			List<SpreadEntry> list = TimeSpreadCalc.DiffList;
			int y = e.Y - EDGE_OFFSET;
			if (y < 0)
			{
				y = 0;
			}
			else if (y >= ClientRectangle.Height - EDGE_OFFSET * 3)
			{
				y = list.Count - 1;
			}
			else
			{
				y = y / this.rectHeight;
			}

			lock (this.monitor)
			{
				if (y >= list.Count || y < 0)
				{
					return null;
				}
				return list[y];
			}
		}

		private void DragContrast(MouseEventArgs e)
		{
			if (this.lastMouseY == 0)
			{
				this.lastMouseY = this.lastMouseY = e.Y;
				return;
			}
			this.timeSpreadCalc.Contrast = this.timeSpreadCalc.Contrast + ((this.lastMouseY - e.Y) * 5);
			this.lastMouseY = e.Y;
		}

		public delegate void LineSelectedEventHandler(object sender, SelectLineEventArgs e);

		public event LineSelectedEventHandler LineSelected;

		private void OnLineSelected(SelectLineEventArgs e)
		{
			if (LineSelected != null)
				LineSelected(this, e);
		}
	}
}