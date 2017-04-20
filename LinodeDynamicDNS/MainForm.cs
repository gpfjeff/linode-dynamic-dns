/* MainForm.cs
 * 
 * PROGRAMMER:    Jeffrey T. Darlington
 * DATE:          February 18, 2016
 * PROJECT:       Linode Dynamic DNS Updater
 * .NET VERSION:  2.0
 * 
 * This file represents the code behind the main UI of the Linode Dynamic DNS Updater.  This is
 * the "setup" mode, where the user can enter their Linode API key and choose which domains
 * will get associated with the machine's outward facing IP address.
 * 
 * The main form includes a text box where the user can enter their API key, as well as a button
 * that allows them to test that the API key works.  This key is stored in the registry, where
 * the "batch" mode will be able to read it.
 * 
 * There is also a list box that shows the current list of domains that have been chosen to be
 * updated.  This list also is stored in the registry.  The Add button launches the AddDialog,
 * where the user can select the domain from a list of available domains and pass it back to be
 * displayed in the list (and added to the registry).  The Remove button allows domains to be
 * removed from the list (and the registry).
 * 
 * The Update Now button essentially mimics the the "batch" mode, performing the update to
 * Linode's DNS and displaying the results in a dialog.
 * 
 * There's also the prerequisite About, Help, and Close buttons.  Huzzah.
 * 
 * This program is Copyright 2017, Jeffrey T. Darlington.
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
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace com.gpfcomics.LinodeDynamicDNS
{
    /// <summary>
    /// This class encapsulates the functionality of our main UI.  Through this form, the user will be able to modify the registry
    /// settings used by the app in Scheduled Task mode.  The user can set their Linode API key, as well as select which domains
    /// they want to update.  These values will be stored in the registry.  Then when the program is run via a Scheduled Task, the
    /// registry settings will be read and acted upon.
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>
        /// A reference to our root registry key.  Everything we do will be stored under this key.
        /// </summary>
        private RegistryKey registryRoot = null;

        /// <summary>
        /// A reference to the Domains registry subkey, which is under our root.  This is where we'll store our list of
        /// domains.
        /// </summary>
        private RegistryKey domainSubkeys = null;

        /// <summary>
        /// This character array will be used to split the domain and resource IDs that are stored in the registry.
        /// </summary>
        private char[] resourceSplitter = { ',' };

        /// <summary>
        /// This string array will be used to split API error responses so we can display intelligent errors.
        /// </summary>
        private string[] errorSplitter1 = { "\"ERRORMESSAGE\":\"" };

        /// <summary>
        /// This string array will be used to split API error responses so we can display intelligent errors.
        /// </summary>
        private string[] errorSplitter2 = { "\"" };

        /// <summary>
        /// The expected result from the Linode API for a successful update.  The "{0}" token should be replaced with the
        /// resource ID of the domain being updated.
        /// </summary>
        private string EXPECTED_RESPONSE = "{\"ACTION\":\"domain.resource.update\",\"DATA\":{\"ResourceID\":{0}},\"ERRORARRAY\":[]}";

        /// <summary>
        /// Our main constructor
        /// </summary>
        public MainForm()
        {
            // Perform the usual initialization:
            InitializeComponent();

            // Asbestos underpants:
            try
            {
                // Try to open our root registry key, creating it if it doesn't exist.  We'll put our master key
                // under HKCU\Software.  As per the usual custom, we'll create a "master" key for our "company"
                // ("GPF Comics") then create a key for our app under that.  In the end, the only key we're
                // interested in is the app's root key, so we'll close everything above it once we no longer
                // need it.
                RegistryKey HKCU_Software = Registry.CurrentUser.OpenSubKey("Software", true);
                if (HKCU_Software != null)
                {
                    RegistryKey GPF = HKCU_Software.OpenSubKey(Properties.Resources.GPFRegKeyName, true);
                    if (GPF == null) GPF = HKCU_Software.CreateSubKey(Properties.Resources.GPFRegKeyName);
                    if (GPF != null)
                    {
                        registryRoot = GPF.OpenSubKey(Properties.Resources.UpdaterRegKeyName, true);
                        if (registryRoot == null) registryRoot = GPF.CreateSubKey(Properties.Resources.UpdaterRegKeyName);
                        GPF.Close();
                    }
                    HKCU_Software.Close();
                }

                // By this point, we either have our root key or we don't.  If we've got it, proceed:
                if (registryRoot != null)
                {
                    // Try to get our API key from the registry if it's present, then put it in the API text box:
                    txtAPIKey.Text = (string)registryRoot.GetValue(Properties.Resources.APIKeyRegKeyName);
                    // If we couldn't find a key, disable the Add button until the user supplies one:
                    if (txtAPIKey.Text.Length > 0)
                    {
                        btnAdd.Enabled = true;
                        btnTestAPIKey.Enabled = true;
                    }

                    // Try to update the "last run" label with the value from the registry.  This gets set whenever
                    // the program runs in batch mode and it successfully opens the registry.  If the value doesn't
                    // exist, we'll assume that the job has never run, which is the default for the label.
                    try
                    {
                        string lastRan = (string)registryRoot.GetValue(Properties.Resources.LastRanRegKeyName);
                        if (!String.IsNullOrEmpty(lastRan)) lblLastRan.Text = "Last Job Ran: " + lastRan;
                    }
                    catch { }

                    // Similarly, try to get the success/failed status of the last job run.  Again, this only gets
                    // set when the program is run in batch mode and we're able to access the registry.  If the value
                    // can't be found, we'll assume it's never been run.  Note that the value is actually a DWORD,
                    // but we're first casting it to a string to make sure it's populated.  Then we'll parse it back
                    // to an integer and recast it back to an actual boolean before testing it.
                    try
                    {
                        int success1 = (int)registryRoot.GetValue(Properties.Resources.SuccessRegKeyName);
                        if (success1 == 0) lblSuccess.Text = "Last Run Status: Successful";
                        else lblSuccess.Text = "Last Run Status: Failed";
                    }
                    catch { }

                    // Try to open the Domains subkey, again trying to create it if it doesn't exist:
                    domainSubkeys = registryRoot.OpenSubKey(Properties.Resources.DomainSubKeyName, true);
                    if (domainSubkeys == null) domainSubkeys = registryRoot.CreateSubKey(Properties.Resources.DomainSubKeyName);
                    if (domainSubkeys != null)
                    {
                        // If they exist, get the list of existing domains in the subkey, sort them, and then add each
                        // domain to the list box:
                        string[] domains = domainSubkeys.GetValueNames();
                        if (domains != null && domains.Length > 0)
                        {
                            Array.Sort(domains, StringComparer.InvariantCulture);
                            foreach (string domain in domains)
                            {
                                listBox1.Items.Add(domain);
                            }
                        }
                    }
                    // If we couldn't get our domains subkey, aboart with an error message:
                    else
                    {
                        MessageBox.Show("Unable to open or create our domains registry subkey, so can't save our settings!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Close();
                    }

                    // By default, the Update button is disabled.  Enable it if and only if the API key appears to be
                    // present and there's at least one domain in the list box:
                    if (!String.IsNullOrEmpty(txtAPIKey.Text) && listBox1.Items.Count > 0)
                        btnUpdate.Enabled = true;

                }
                // If we couldn't get our root registry key, abort with an error message:
                else
                {
                    MessageBox.Show("Unable to open or create our root registry key, so can't save our settings!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                }
            }
            // If anything blew up, complain then exit:
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }
        
        /// <summary>
        /// What to do when the application closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // The main thing we want to do here is make sure that we close our registry keys.  This also makes
            // sure that any unsaved changes are saved.
            if (domainSubkeys != null) domainSubkeys.Close();
            if (registryRoot != null) registryRoot.Close();
        }

        /// <summary>
        /// What to do when the API key text box value changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtAPIKey_TextChanged(object sender, EventArgs e)
        {
            // Asbestos underpants:
            try
            {
                // Run the API key text through a regex to make sure it's only alphanumerics:
                txtAPIKey.Text = Regex.Replace(txtAPIKey.Text, "![0-9a-zA-Z]", "", RegexOptions.None);
                // This only makes sense if the API key string is not empty:
                if (txtAPIKey.Text.Length > 0)
                {
                    // By this point, the root registry key *should* be open, but we'll double-check, just in case:
                    if (registryRoot != null)
                    {
                        // Take the API key value from the text box and stuff it into the registry:
                        registryRoot.SetValue(Properties.Resources.APIKeyRegKeyName, txtAPIKey.Text, RegistryValueKind.String);
                        registryRoot.Flush();
                    }
                    // Enable the Add, Test, and (if we have any domains chosen) Update buttons:
                    btnAdd.Enabled = true;
                    btnTestAPIKey.Enabled = true;
                    if (listBox1.Items.Count > 0) btnUpdate.Enabled = true;
                    else btnUpdate.Enabled = false;
                }
                // If the API key box is empty, disable the Add, Test, and Update buttons:
                else
                {
                    btnAdd.Enabled = false;
                    btnTestAPIKey.Enabled = false;
                    btnUpdate.Enabled = false;
                }
            }
            // If anything blows up, complain:
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        ///  What to do when the Close button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClose_Click(object sender, EventArgs e)
        {
            // Well, this should be obvious:
            this.Close();
        }

        /// <summary>
        /// What to do when the Add button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            // Asbestos underpants:
            try
            {
                // Build the Add Domain dialog, passing it our API key, and show it.  If the user clicks OK,
                // proceed to the next step:
                AddDialog ad = new AddDialog(txtAPIKey.Text);
                if (ad.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // It only makes sense to add a domain to our list if we haven't already added it:
                    if (!listBox1.Items.Contains(ad.SelectedDomain))
                    {
                        // Create a registry value for the new domain, using the domain itself as the name and
                        // the domain and resource IDs as the value.  To store the two IDs, we'll conver them to
                        // strings and glue them together with a comma.  Then flush the key to save the value to disk.
                        string ids = ad.DomainID.ToString() + "," + ad.ResourceID.ToString();
                        domainSubkeys.SetValue(ad.SelectedDomain, ids, RegistryValueKind.String);
                        domainSubkeys.Flush();
                        // Add the domain to the list box.  If we want to be really OCD, we could probably
                        // sort this list so it's always alphabetical.
                        listBox1.Items.Add(ad.SelectedDomain);
                    }
                }
            }
            // If anything blows up:
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // If we added a domain, we'll want to enable the Update button as well.  However, since the above could
            // blow up somewhere, we'll test the list box first and make sure we have at least one domain:
            if (listBox1.Items.Count > 0) btnUpdate.Enabled = true;
            else btnUpdate.Enabled = false;
        }

        /// <summary>
        /// What to do when the Remove button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRemove_Click(object sender, EventArgs e)
        {
            // Asbestos underpants:
            try
            {
                // The Remove button should be disabled if nothing has been selected, but we'll double-check
                // and make sure we have something to remove before trying it:
                if (listBox1.SelectedItems.Count > 0)
                {
                    // We can't modify the list box while iterating over the selected items, so copy the
                    // selected domains into a string array, then loop over the array:
                    string[] items = new string[listBox1.SelectedItems.Count];
                    int i = 0;
                    foreach (string item in listBox1.SelectedItems)
                    {
                        items[i] = item;
                        i++;
                    }
                    foreach (string item in items)
                    {
                        // Delete the registry key for the domain, then remove its item from the list box:
                        domainSubkeys.DeleteValue(item);
                        listBox1.Items.Remove(item);
                    }
                    // Flush the domains registry key to save our changes:
                    domainSubkeys.Flush();
                }
                // Disable the remove button.  Nothing should be selected at this point.
                btnRemove.Enabled = false;
            }
            // If anything blew up:
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Removing a domain could bring our list of domains down to zero.  If we don't have any domains
            // left in the list box, disable the Update button.  If there's still some in there, make sure
            // the button is still enabled:
            if (listBox1.Items.Count > 0) btnUpdate.Enabled = true;
            else btnUpdate.Enabled = false;
        }

        /// <summary>
        /// What to do when the About button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAbout_Click(object sender, EventArgs e)
        {
            AboutDialog ad = new AboutDialog();
            ad.ShowDialog();
        }

        /// <summary>
        /// What to do when the Help button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This feature has not been implemented yet!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// What to do when the list box selection changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Check to see how many items are selected in the list box.  If nothing is selected, disable
            // the Remove button.  Otherwise, enable the Remove button.
            if (listBox1.SelectedIndices.Count == 0) btnRemove.Enabled = false;
            else btnRemove.Enabled = true;
        }

        /// <summary>
        /// What to do when the Test button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnTestAPIKey_Click(object sender, EventArgs e)
        {
            // In order to test our API key, we'll need a WebClient to probe the API:
            WebClient webClient = null;
            // Asbestos underpants:
            try
            {
                // Create the WebClient, then attempt to access the API using the "test.echo" command.  All this command does is echo back
                // whatever we send it, but since it requires an API key, we'll be able to test that the key is valid.
                webClient = new WebClient();
                string response = webClient.DownloadString(Properties.Resources.APICallHeader + txtAPIKey.Text + "&api_action=test.echo&foo=bar");
                // If we get back the expected response, then we'll know the API key the user entered is valid.  Otherwise, it's not.
                // Show the appropriate message.  Also note that if the API isn't valid, we'll disable the Add button; we don't want
                // the user trying add domains to the list if we don't have a valid API key.
                if (!String.IsNullOrEmpty(response) && response.CompareTo("{\"ACTION\":\"test.echo\",\"DATA\":{\"foo\":\"bar\"},\"ERRORARRAY\":[]}") == 0)
                    {
                    MessageBox.Show("Your API key appears to be valid!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    btnAdd.Enabled = true;
                    // We also need to potentially enable the Update button... assuming there's some domains in our list box:
                    if (listBox1.Items.Count > 0) btnUpdate.Enabled = true;
                    else btnUpdate.Enabled = false;
                }
                else
                {
                    MessageBox.Show("Your API key does not appear to be valid!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnAdd.Enabled = false;
                    btnUpdate.Enabled = false;
                }
            }
            // If anything blows up, complain:
            catch (Exception ex1)
            {
                MessageBox.Show(ex1.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            // Dispose of our WebClient, if appropriate:
            if (webClient != null) webClient.Dispose();
        }

        /// <summary>
        /// What to do if the Update button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnUpdate_Click(object sender, EventArgs e)
        {
            // We'll need a WebClient to call the Linode API:
            WebClient webClient = null;
            // How we display our results depends on whether or not we were successful.  We'll be optimistic and assume
            // success, then adjust our expectations accordingly.
            bool success = true;
            // We'll also want to record a running status message to display to the user.  We'll stuff all our statuses
            // into this string.
            string message = "Interactive Update Results:\n\n";
            // Turn on the wait cursor, just in case we have to leave the user waiting:
            UseWaitCursor = true;
            // Asbestos underpants:
            try
            {
                // Update the "last run" registry entry with the current timestamp:
                registryRoot.SetValue(Properties.Resources.LastRanRegKeyName, DateTime.Now.ToString(), RegistryValueKind.String);

                // This string will hold the actual URL for our update call.  The "{0}" and "{1}" tokens will be replaced with
                // the domain and resource IDs respectively for the domain being updated.  "[remote_addr]" is a special token
                // recognized by the Linode API that basically means "look at the IP that made this request and stuff that value
                // in here".
                string updateURL = Properties.Resources.APICallHeader +
                    (string)registryRoot.GetValue(Properties.Resources.APIKeyRegKeyName) +
                    "&api_action=domain.resource.update&DomainID={0}&ResourceID={1}&Target=[remote_addr]";

                // Check the domains subkey and see how many values are out there.  It only makes sense to run this if there
                // are domains to work with:
                if (domainSubkeys.GetValueNames().Length > 0)
                {
                    // Build our WebClient:
                    webClient = new WebClient();

                    // Loop through the domains under the domains subkey:
                    foreach (string domain in domainSubkeys.GetValueNames())
                    {
                        // Add the domain to the status message:
                        message += domain + ": ";
                        // Yes, we're using another try/catch block.  The hope here is that if one domain fails, we can try the
                        // other domains in the list individually.
                        try
                        {
                            // Get the IDs from the registry, split them apart, and then stuff them into our request URL and call
                            // the API:
                            string idstemp = (string)domainSubkeys.GetValue(domain);
                            string[] ids = idstemp.Split(resourceSplitter);
                            string response = webClient.DownloadString(updateURL.Replace("{0}", ids[0]).Replace("{1}", ids[1]));
                            // Check our response.  If it's not empty and it matches what we expect (note that we have to include
                            // the resource ID into our check), then we'll assume success:
                            if (!String.IsNullOrEmpty(response) && response.CompareTo(EXPECTED_RESPONSE.Replace("{0}", ids[1])) == 0)
                            {
                                message += "SUCCESS!\n";
                            }
                            // If we didn't get what we expected, mark this as a failure:
                            else
                            {
                                message += "FAILED!\n";
                                success = false;
                                // If we got an error message from Linode, try to isolate it.  Hopefully that will help us debug the
                                // problem later.
                                if (!String.IsNullOrEmpty(response) && response.Contains("ERRORMESSAGE"))
                                {
                                    string[] errorbits1 = response.Split(errorSplitter1, StringSplitOptions.RemoveEmptyEntries);
                                    string[] errorbits2 = errorbits1[1].Split(errorSplitter2, StringSplitOptions.RemoveEmptyEntries);
                                    message += "API ERROR: " + errorbits2[0] + "\n";
                                }
                            }
                        }
                        // If something blew up for this domain, add the error to our message and move on:
                        catch (Exception ex1)
                        {
                            message += "FAILED!\n";
                            message += ex1.ToString()+ "\n";
                            success = false;
                        }
                    }
                }
                // This shouldn't happen, but if we couldn't find any domains to update, abort:
                else
                {
                    message += "ERROR: No domains found to be updated!\n";
                    success = false;
                }

                // Update the "success" registry key with our success status.  Note that we can only do this if the
                // registry root is open and available, so only errors caught above will set this.  Errors caught
                // below are on their own.
                registryRoot.SetValue(Properties.Resources.SuccessRegKeyName, success ? 0 : 1, RegistryValueKind.DWord);

                // Update our "last ran" and "status" labels:
                lblLastRan.Text = "Last Job Ran: " + (string)registryRoot.GetValue(Properties.Resources.LastRanRegKeyName);
                if (success) lblSuccess.Text = "Last Run Status: Successful";
                else lblSuccess.Text = "Last Run Status: Failed";

            }
            // Our catch-all exception.  If anything blew up that wasn't on a specific domain, add that error message to
            // our output:
            catch (Exception ex)
            {
                message += ex.ToString() + "\n";
                success = false;
            }
            // Turn off the wait cursor:
            UseWaitCursor = false;

            // Show our results.  Note that the success flag mainly determines our dialog icon and title.  The message
            // will display whatever we stuffed into it along the way.
            if (success) MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else MessageBox.Show(message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
