/* AboutDialog.cs
 * 
 * PROGRAMMER:    Jeffrey T. Darlington
 * DATE:          February 18, 2016
 * PROJECT:       Linode Dynamic DNS Updater
 * .NET VERSION:  2.0
 * 
 * This displays the About dialog for our Linode Dynamic DNS Updater.  It's not very fancy, but
 * it does the job.  Most of the metadata comes from the assembly properties or from the
 * project resources; retrieving that takes up the bulk of the code in the constructor.
 * Otherwise, the most interesting bit is the link button that launches the user's default browser.
 * This works well enough on Windows, but I'm not sure how well it works on non-Windows systems.
 * 
 * This program is Copyright 2016, Jeffrey T. Darlington.
 * E-mail:  jeff@gpf-comics.com
 * Web:     https://github.com/gpfjeff/linode-dynamic-dns
 * 
 * This program is free software; you can redistribute it and/or modify it under the terms of
 * the GNU General Public License as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 * See theGNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License along with this program;
 * if not, write to the Free Software Foundation, Inc., 51 Franklin Street, Fifth Floor,
 * Boston, MA  02110-1301, USA.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace com.gpfcomics.LinodeDynamicDNS
{
    public partial class AboutDialog : Form
    {
        public AboutDialog()
        {
            InitializeComponent();
            lblVersion.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            lblLink.Text = Properties.Resources.WebsiteURL;
            try
            {
                string copyright = null;
                object[] obj = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (obj != null && obj.Length > 0)
                    copyright = ((AssemblyCopyrightAttribute)obj[0]).Copyright;
                if (!String.IsNullOrEmpty(copyright))
                    lblCopyright.Text = copyright;
            }
            catch
            {
                lblCopyright.Text = "";
            }

        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void lblLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try { System.Diagnostics.Process.Start(lblLink.Text); }
            catch
            {
                MessageBox.Show("I was unable to launch your default browser to open " +
                    "the website. Please open your browser and type in the " +
                    "URL manually.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
    }
}
