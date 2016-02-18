/* Program.cs
 * 
 * PROGRAMMER:    Jeffrey T. Darlington
 * DATE:          February 18, 2016
 * PROJECT:       Linode Dynamic DNS Updater
 * .NET VERSION:  2.0
 * 
 * The Linode Dynamic DNS Updater is a small .NET 2.0 application that allows the user to use
 * Linode's awesome DNS and API as a sort of "dynamic DNS", similar to services offered by other
 * companies.  The advantage, of course, is that if the user is a Linode customer, they can take
 * advantage of Linode's existing DNS service to do the same thing, updating the IP address of
 * the "dynamic" machine via Linode's API.
 * 
 * This file is the main entry point of the program.  The program can be called in one of two modes.
 * By default, the program enters and interactive, GUI-driven "setup" mode that allows to user to
 * configure the program.  This includes specifying their Linode API key and selecting which domain
 * names will be associated with the dynamic IP.  The program assumes that the user has already
 * applied for an API key with Linode and that all of the domains that will be associated with the
 * dynamic IP are already configured within Linode's DNS Manager.
 * 
 * If run with the "-run" command-line flag (case insensitive), the program runs in "batch" mode.
 * Assuming that the program has already been configured via the GUI, batch mode performs the
 * actual update to Linode's DNS using the API.  Status messages are printed to the console, which
 * is convenient if the program is being run directly via the command line, or on non-Windows
 * UNIX-like systems that run via a scheduler like cron.  Under Windows, the program can be run
 * as a Scheduled Task, but the user will not see any output.  As such, the last run date and its
 * success status will also be logged in the registry and can be displayed by running the program
 * in "setup" mode.
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
using System.IO;
using System.Net;
using System.Windows.Forms;
using Microsoft.Win32;

namespace com.gpfcomics.LinodeDynamicDNS
{
    /// <summary>
    /// Our main application.  This program can run in one of two modes.  The first is an interactive, GUI-driven "setup"
    /// mode, where the user enters their Linode API key and selects which domains they wish to update.  This is the default
    /// mode for this app, and is launched whenever the app is started without any command-line arguments (or, more correctly,
    /// without the expected command-line arguments).  If the app is called with the "-run" flag on the command line (which
    /// is case insensitive), the app then enters "batch" mode, where it will attempt to update the domains specified by
    /// the user when the program was run interactively.  The batch mode expects the API key and the domain list to be in
    /// the Windows registry and will fail if it cannot find them.
    /// </summary>
    static class Program
    {
        /// <summary>
        /// The expected result from the Linode API for a successful update.  The "{0}" token should be replaced with the
        /// resource ID of the domain being updated.
        /// </summary>
        private static string EXPECTED_RESPONSE = "{\"ERRORARRAY\":[],\"DATA\":{\"ResourceID\":{0}},\"ACTION\":\"domain.resource.update\"}";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            // Check to see if we've been given the non-interactive, batch mode flag ("-run", case insensitive).  If we
            // have, run the program in batch mode and attempt to update the domains saved in the registry.
            if (args != null && args.Length == 1 && args[0].ToUpper() == "-RUN")
            {
                // Declare a few variables to get us started.  Since they'll only be used during batch mode, there's no
                // need to declare them as class variables; we'll just make them local to this branch.  We'll start with
                // some references to our main registry keys, namely our root and the domains subkey.  We'll need access
                // to both before we can proceed.
                RegistryKey registryRoot = null;
                RegistryKey domainSubkeys = null;
                // We'll need a WebClient to call the Linode API:
                WebClient webClient = null;
                // And we'll need to keep track of our success throughout the process.  We'll be optimisitic and assume
                // that we'll succeed, then update this if things go south.
                bool success = true;
                // This character array will be used to split the domain and resource IDs that are stored in the registry.
                char[] resourceSplitter = { ',' };
                // These string arrays will be used to split API error responses so we can display intelligent errors.
                string[] errorSplitter1 = { "\"ERRORMESSAGE\":\"" };
                string[] errorSplitter2 = { "\"" };

                // Put on our asbestos underpants:
                try
                {
                    // We'll write some output to the console, just in case the user wants it.  Under normal circumstances
                    // on Windows, this will probably be ignored.  However, we'll still provide feedback, just in case the
                    // user tries to run this directly via the command line.  On non-Windows platforms, running this via
                    // a cron or some other scheduler will provide a lot that could be e-mailed to the cron owner.
                    Console.WriteLine("\nBegin " + Application.ProductName + " v" + Application.ProductVersion + " at " +
                        DateTime.Now.ToString());

                    // Attempt to open our registry keys.  We'll want write access to the root key, while we only need
                    // read-only access to the domain subkey.  Note that unlike the GUI setup mode, we won't try to create
                    // the keys if they don't exist.
                    RegistryKey HKCU_Software = Registry.CurrentUser.OpenSubKey("Software", false);
                    if (HKCU_Software != null)
                    {
                        RegistryKey GPF = HKCU_Software.OpenSubKey(Properties.Resources.GPFRegKeyName, false);
                        if (GPF != null)
                        {
                            registryRoot = GPF.OpenSubKey(Properties.Resources.UpdaterRegKeyName, true);
                            if (registryRoot != null)
                            {
                                domainSubkeys = registryRoot.OpenSubKey(Properties.Resources.DomainSubKeyName, false);
                            }
                            GPF.Close();
                        }
                        HKCU_Software.Close();
                    }

                    // Everything depends on our registry keys being available:
                    if (registryRoot != null && domainSubkeys != null)
                    {
                        // Update the "last run" registry entry with the current timestamp:
                        registryRoot.SetValue(Properties.Resources.LastRanRegKeyName, DateTime.Now.ToString(), RegistryValueKind.String);

                        // Check to make sure that the API key is in the registry:
                        if (!String.IsNullOrEmpty((string)registryRoot.GetValue(Properties.Resources.APIKeyRegKeyName)))
                        {
                            // Build our API request URL.  Basically, this is just the "header" (the protocol and domain name of the API),
                            // followed by our API key, and the rest of the REST commands to tell Linode to update the address for the
                            // domain.  There are two things to note here.  First, the "{0}" and "{1}" tokens are placeholders which we'll
                            // substitute with the domain ID and resource ID, respectively, right before we make the actual request.
                            // Secondly, the "[remote_addr]" token is a special tag recognized by the Linode API that basically tells Linode
                            // "use whatever IP address this request is coming from".  I've seen a number of other apps like this that go out
                            // and fetch the IP from some other resource or service when that really isn't necessary; Linode's got that built
                            // right into the API.
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
                                    // For each domain, log which domain we're trying to update, then give it a try.  Start by replacing
                                    // the tokens in the URL with the domain and resource IDs, then use the WebClient to call the IP.  Check
                                    // the response against what we expect, again plugging the resource ID into the appropriate place.
                                    // If everything looks good, huzzah!  If anything fails, boo!  Note that while we have an overall
                                    // try/catch block, we'll set up our own internal one where in the hope that we can keep looping
                                    // if just a single domain fails.
                                    Console.Write("Setting IP for " + domain + "... ");
                                    try
                                    {
                                        string idstemp = (string)domainSubkeys.GetValue(domain);
                                        string[] ids = idstemp.Split(resourceSplitter);
                                        string response = webClient.DownloadString(updateURL.Replace("{0}", ids[0]).Replace("{1}", ids[1]));
                                        if (!String.IsNullOrEmpty(response) && response.CompareTo(EXPECTED_RESPONSE.Replace("{0}", ids[1])) == 0)
                                        {
                                            Console.WriteLine("SUCCESS!");
                                        }
                                        else
                                        {
                                            Console.WriteLine("FAILED!");
                                            success = false;
                                            // If we got an error message from Linode, try to isolate it and print that error to the console.
                                            // Hopefully that will help us debug the problem later.
                                            if (!String.IsNullOrEmpty(response) && response.Contains("ERRORMESSAGE"))
                                            {
                                                string[] errorbits1 = response.Split(errorSplitter1, StringSplitOptions.RemoveEmptyEntries);
                                                string[] errorbits2 = errorbits1[1].Split(errorSplitter2, StringSplitOptions.RemoveEmptyEntries);
                                                Console.WriteLine("API ERROR: " + errorbits2[0]);
                                            }
                                        }
                                    }
                                    catch (Exception ex1)
                                    {
                                        Console.WriteLine("FAILED!");
                                        Console.WriteLine(ex1.ToString());
                                        success = false;
                                    }
                                }
                            }
                            // If we couldn't find any domains to update, abort:
                            else
                            {
                                Console.WriteLine("\nERROR: No domains found to be updated!  Aborting...");
                                success = false;
                            }
                        }
                        // If we couldn't find the API key in the registry, abort:
                        else
                        {
                            Console.WriteLine("\nERROR: Linode API key has not been set!  Aborting...");
                            success = false;
                        }

                        // Update the "success" registry key with our success status.  Note that we can only do this if the
                        // registry root is open and available, so only errors caught above will set this.  Errors caught
                        // below are on their own.
                        registryRoot.SetValue(Properties.Resources.SuccessRegKeyName, success ? 0 : 1, RegistryValueKind.DWord);
                    }
                    // If we couldn't open both of our registry keys, abort and complain:
                    else
                    {
                        Console.WriteLine("\nERROR: Unable to open registry keys!  Aborting...");
                        success = false;
                    }

                    // Dispose of the WebClient and close our registry keys, if they're still available:
                    if (webClient != null) webClient.Dispose();
                    if (domainSubkeys != null) domainSubkeys.Close();
                    if (registryRoot != null) registryRoot.Close();

                    // Close out our logging and return our exit code.  Note that we'll following the old DOS/UNIX
                    // convention of returning zero for success and one for any error.  We could probably get fancy
                    // and return different error codes for different errors, but... well, that's more work.
                    Console.WriteLine("\nEnd " + Application.ProductName + " v" + Application.ProductVersion + " at " +
                        DateTime.Now.ToString());
                    if (success) return 0;
                    else return 1;
                }
                // If anything blows up:
                catch (Exception ex)
                {
                    // Dispose of the WebClient and close our registry keys, if they're still available:
                    if (webClient != null) webClient.Dispose();
                    if (domainSubkeys != null) domainSubkeys.Close();
                    if (registryRoot != null) registryRoot.Close();

                    // Close out our log.  Note that, technically, this could throw some IO exceptions, so we'll
                    // put this in a try/catch block, just in case.  However, we'll silently fail if we can't
                    // print anything; after all, if we can't write to the console, we're pretty much out of
                    // logging options.
                    try
                    {
                        Console.WriteLine("\n" + ex.ToString());
                        Console.WriteLine("\nEnd " + Application.ProductName + " v" + Application.ProductVersion + " at " +
                            DateTime.Now.ToString());
                    }
                    catch { }
                    // Any exception thrown means there was an error, so return one:
                    return 1;
                }
            }

            // If we weren't given the batch-mode flag, run the program in interactive setup mode with the GUI:
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
        }
    }
}
