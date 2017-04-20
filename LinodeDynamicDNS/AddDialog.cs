/* AddDialog.cs
 * 
 * PROGRAMMER:    Jeffrey T. Darlington
 * DATE:          February 18, 2016
 * PROJECT:       Linode Dynamic DNS Updater
 * .NET VERSION:  2.0
 * 
 * The Add Dialog takes the API key handed to it by the main form, then queries the Linode API
 * to bring back a list of domains.  The domains are displayed as a tree, starting at the top
 * domain level (think DNS zones).  Clicking on a domain/zone expands it to show all A and AAAA
 * records associated with it, including explicit and "naked" subdomains (i.e. "example.com"
 * as well as "www.example.com").  Only domains/zones where Linode is the "master" DNS are
 * returned, since "slave" DNS records at Linode would be overwritten by whoever controls the
 * "master" zone.
 * 
 * Once a domain is selected, the Add button passes the selected domain and its associated
 * resource IDs back to the main form, where it will be added to the registry.  The Cancel button
 * closes this dialog without returning the data.
 * 
 * Note that it's up to the user to set up the DNS records in Linode's DNS Manager first, and
 * for them to know whether they're using an IPv4 ("A") or IPv6 ("AAAA") outward-facing IP.
 * This app will happily tell Linode to use whatever IP the request came from, but if there isn't
 * a valid address available for the record type, I have no idea what will happen.
 * 
 * I also can't be held responsible for anyone who "accidentally" overwrites their main webserver's
 * domain with their home cable modem's IP.  It's up to *YOU* to pick the right domain! ;)
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace com.gpfcomics.LinodeDynamicDNS
{
    /// <summary>
    /// This form encapsulates the functionality of the Add Domain dialog box.  This dialog actually talks to the Linode API and fetches
    /// the domain names and their resource IDs for us to save to the registry for our later scheduled updates.
    /// </summary>
    public partial class AddDialog : Form
    {
        /// <summary>
        /// Our local copy of the Linode API key.  This is passed to us by our constructor.
        /// </summary>
        private string apiKey = null;

        /// <summary>
        /// The currently selected domain name.
        /// </summary>
        private string selectedDomain = null;

        /// <summary>
        /// The currently selected domain's resource ID number.
        /// </summary>
        private long resourceID = 0;

        /// <summary>
        /// The currently selected domain's domain (i.e. zone) ID number.
        /// </summary>
        private long domainID = 0;

        /// <summary>
        /// A WebClient for accessing the Linode API.
        /// </summary>
        private WebClient webClient = null;

        /// <summary>
        /// A String array for use in the String.Split() command.  This splitter is used to split individual domain records apart
        /// in the returned results from the API.
        /// </summary>
        private string[] domainListSplitter = { "},{" };

        /// <summary>
        /// A String array for use in the String.Split() command.  This splitter is used to split the various attributes of a given
        /// domain record.
        /// </summary>
        private string[] domainEntrySplitter = { "," };

        /// <summary>
        /// A String array for use in the String.Split() command.  This splitter is used to split name/value pairs in a given
        /// domain record attribue.
        /// </summary>
        private string[] domainBitSplitter = { ":" };

        /// <summary>
        /// The selected domain name
        /// </summary>
        public string SelectedDomain
        {
            get
            {
                return selectedDomain;
            }
        }

        /// <summary>
        /// The selected domain's resource ID
        /// </summary>
        public long ResourceID
        {
            get
            {
                return resourceID;
            }
        }

        /// <summary>
        /// The currently selected domain's domain (i.e. zone) ID number.
        /// </summary>
        public long DomainID
        {
            get
            {
                return domainID;
            }
        }

        /// <summary>
        /// Our constructor
        /// </summary>
        /// <param name="apiKey">The Linode API key</param>
        public AddDialog(string apiKey)
        {
            // Start by doing the usual initialization, then taking note of the API key.  We'll also disable the
            // Add button to make sure it doesn't get pressed until we have something to show for ourselves.
            InitializeComponent();
            this.apiKey = apiKey;
            btnAdd.Enabled = false;

            // Asbestos underpants:
            try
            {
                // Build the API URL to get the top-level domain list, then use the WebClient to fetch our response:
                string domainListURL = Properties.Resources.APICallHeader + apiKey + "&api_action=domain.list";
                webClient = new WebClient();
                string response = webClient.DownloadString(domainListURL);
                // If we got anything useful:
                if (response.EndsWith(",\"ERRORARRAY\":[]}"))
                {
                    // To more easily sort things, we'll store our parsed list in a Hashtable.  Initialize it now:
                    Hashtable domains = new Hashtable();
                    // Strip off the "header" and "footer" from the result, then split it using the first record splitter.  This
                    // should give us a string array where each entry is a full top-level domain record.
                    response = response.Replace("{\"ACTION\":\"domain.list\",\"DATA\":[{", "").Replace("}],\"ERRORARRAY\":[]}", "");
                    string[] domainList = response.Split(domainListSplitter, StringSplitOptions.RemoveEmptyEntries);
                    // Loop through each domain record:
                    foreach (string domainEntry in domainList)
                    {
                        // Since it doesn't make much sense to work on DNS slave records, we'll only work with master records:
                        if (domainEntry.ToUpper().Contains("\"TYPE\":\"MASTER\""))
                        {
                            // We'll need to keep track of the domain name and the domain ID, so declare some variables to hold them:
                            string domain = null;
                            string id = null;
                            // Split the domain record again, this time to get the individual name/value pairs.  Loop through those:
                            string[] domainBits = domainEntry.Split(domainEntrySplitter, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string bit in domainBits)
                            {
                                // If we found the domain ID pair, isolate the ID and take note of it:
                                if (bit.ToUpper().StartsWith("\"DOMAINID\""))
                                {
                                    string[] bitParts = bit.Split(domainBitSplitter, StringSplitOptions.RemoveEmptyEntries);
                                    id = bitParts[1];
                                }
                                // If we found the domain name pair, isolate the name:
                                else if (bit.ToUpper().StartsWith("\"DOMAIN\""))
                                {
                                    string[] bitParts = bit.Split(domainBitSplitter, StringSplitOptions.RemoveEmptyEntries);
                                    domain = bitParts[1].Replace("\"", "");
                                }
                            }
                            // Check our hash table.  If the domain isn't already in the hash, add it and its ID now.  We *shouldn't*
                            // see a domain multiple times, but just in case we do, we'll silently drop subsequent instances since
                            // adding it more than once would throw an exception.
                            if (!domains.ContainsKey(domain)) domains.Add(domain, id);
                        }
                    }
                    // Now we should have a hash table full of domains as the keys and their internal IDs as the values.  If we
                    // got anything at all, proceed:
                    if (domains.Keys.Count > 0)
                    {
                        // While it looks like the list is already sorted, we'll won't assume this is the case and we'll sort our
                        // domains here.  We'll copy our hash table keys to a string array and sort them:
                        domainList = new string[domains.Keys.Count];
                        domains.Keys.CopyTo(domainList, 0);
                        Array.Sort(domainList);
                        // Loop through the domains and build a tree node for each.  The "text" or "label" for the node will be the
                        // domain, while the "name" will be the ID.  Note that while we've sorted this list, we don't know how many
                        // subdomains will be present yet, so we can't show the expand/collapse box.  That will have to come later.
                        foreach (string domain in domainList)
                        {
                            treeView1.Nodes.Add((string)domains[domain], domain);
                        }
                    }
                    // If we didn't find any domains, warn the user and tell them they need to add some domains to their account:
                    else
                    {
                        MessageBox.Show("No MASTER domain zones were found! Make sure to add some domains to your account! (SLAVE zones don't count!)", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        btnAdd.Enabled = false;
                    }
                }
                // If we didn't get the results we expected, throw an error box:
                else
                {
                    MessageBox.Show("Unable to get top domain list from server! Check your API key to make sure it's correct!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnAdd.Enabled = false;
                }
            }
            // If anything blew up, throw an error:
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnAdd.Enabled = false;
            }
        }

        /// <summary>
        /// What to do when a tree node is selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Asbestos underpants:
            try
            {
                // Get a handy reference to our currently selected node:
                TreeNode activeNode = treeView1.SelectedNode;
                // Test to see if it's a top-level node:
                if (activeNode.Parent == null)
                {
                    // Don't let top-level nodes to be added to the registry.  These are actually DNS zones, not domains, so they
                    // aren't what we really want to return.
                    btnAdd.Enabled = false;
                    // Check to see if we've already expanded this node.  If we have, there's no point doing it again.
                    if (activeNode.Nodes.Count == 0)
                    {
                        // Use the wait cursor to let the user know we're working on something:
                        UseWaitCursor = true;

                        // This is very similar to how we populated the top-level domain zones, only our API URL is a bit different.  Our
                        // API action is different (domain.resource.list rather than domain.list) and we need to specify the zone to work
                        // with.  The zone's ID is currently stored in the active node's "name".
                        string domainListURL = Properties.Resources.APICallHeader + apiKey + "&api_action=domain.resource.list&domainid=" + activeNode.Name;
                        // Reuse the WebClient to fetch the zone's domains:
                        webClient = new WebClient();
                        string response = webClient.DownloadString(domainListURL);
                        // If it looks like we got a valid response, proceed:
                        if (response.EndsWith(",\"ERRORARRAY\":[]}"))
                        {
                            // Again, declare a Hashtable to hold our results, chop off the "header" and "footer", then split the domain
                            // records apart and loop through them:
                            Hashtable domains = new Hashtable();
                            response = response.Replace("{\"ACTION\":\"domain.list\",\"DATA\":[{", "").Replace("}],\"ERRORARRAY\":[]}", "");
                            string[] domainList = response.Split(domainListSplitter, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string domainEntry in domainList)
                            {
                                // We're only interested in actual domain records, so ignore everything but A and AAAA records:
                                if (domainEntry.ToUpper().Contains("\"TYPE\":\"A\"") || domainEntry.ToUpper().Contains("\"TYPE\":\"AAAA\""))
                                {
                                    // Split the record into name/value pairs and isolate the resource ID and name values:
                                    string domain = null;
                                    string id = null;
                                    string[] domainBits = domainEntry.Split(domainEntrySplitter, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (string bit in domainBits)
                                    {
                                        if (bit.ToUpper().StartsWith("\"RESOURCEID\""))
                                        {
                                            string[] bitParts = bit.Split(domainBitSplitter, StringSplitOptions.RemoveEmptyEntries);
                                            id = bitParts[1];
                                        }
                                        else if (bit.ToUpper().StartsWith("\"NAME\""))
                                        {
                                            string[] bitParts = bit.Split(domainBitSplitter, StringSplitOptions.RemoveEmptyEntries);
                                            domain = bitParts[1].Replace("\"", "");
                                            // "Naked" domains will have an empty name, while subdomains will probably just be the subdomain
                                            // rather than the full domain (i.e. "www" instead of "www.example.com").  Obviously, we want to
                                            // show the full domain, so appened the domain name from the zone node now.
                                            if (String.IsNullOrEmpty(domain)) domain = activeNode.Text;
                                            else domain = domain + "." + activeNode.Text;
                                            // Tack on a little flag so we'll know whether the record is an IPv4 "A" record or an IPv6 "AAAA"
                                            // record:
                                            if (domainEntry.ToUpper().Contains("\"TYPE\":\"A\"")) domain += " (A)";
                                            else domain += " (AAAA)";
                                        }
                                    }
                                    if (!domains.ContainsKey(domain)) domains.Add(domain, id);
                                }
                            }
                            // If we found anything useful, sort the domains then add each one as a node under the zone's node.  Then
                            // force the zone node to expand so we'll see all the records immediately.
                            if (domains.Keys.Count > 0)
                            {
                                domainList = new string[domains.Keys.Count];
                                domains.Keys.CopyTo(domainList, 0);
                                Array.Sort(domainList);
                                foreach (string domain in domainList)
                                {
                                    activeNode.Nodes.Add((string)domains[domain], domain);
                                }
                                activeNode.Expand();
                            }
                        }

                        // Turn off the wait cursor:
                        UseWaitCursor = false;
                    }
                }

                // If this is not a root-level node, it should correspond to an A or AAAA record within the zone.  These
                // are the type of records we want to return.  So enable the Add button, then take note of the domain name,
                // the resource ID, and the domain ID so if the user clicks Add we'll return these values.  Note that the
                // domain ID is actually the "name" of the currently selected node's parent.
                else
                {
                    btnAdd.Enabled = true;
                    selectedDomain = activeNode.Text;
                    resourceID = Int64.Parse(activeNode.Name);
                    domainID = Int64.Parse(activeNode.Parent.Name);
                }
            }
            // If anything blew up, complain:
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// What to do if the Add button was clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            // Our main concern here is that we notify the caller that we're trying to return a valid domain/ID.  The Add
            // button shouldn't be active unless a valid domain node was selected, so we should have a domain and an ID
            // already recorded.  We'll return OK to the caller, so they'll know to check those properties.
            DialogResult = System.Windows.Forms.DialogResult.OK;
            Close();
        }

        /// <summary>
        /// What to do if the Cancel button was clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            // Here, we want to let the caller know that we're *NOT* returning a result, so we'll set the DialogResult
            // to Cancel.  The caller should then know not to trust the values in the domain/ID properties.
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// What to do when this dialog closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            // The main thing here is to dispose of our WebClient so its resources are freed:
            if (webClient != null) webClient.Dispose();
        }
    }
}
