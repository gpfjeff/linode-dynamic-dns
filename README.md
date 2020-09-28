# Project Status
This project is no longer being actively maintained. The long and short of the matter is that the [Linode API](https://www.linode.com/products/api/) changes far more frequently than I can keep with, and there are better tools out there that have done a better job of keeping up with those changes than I can. Personally, I now both use and recommend the official [Linode CLI](https://github.com/linode/linode-cli), a Python-based command-line utility that uses the Linode API under the hood. I'm not sure how well (or even if) the CLI will run under Windows; I've since moved all of my automated Linode tasks to my personal Linux box, which I know it works well on. I hate to leave anyone stuck on Windows high and dry, especially if the official CLI won't run there, but Linode is pretty Linux-centric anyway, so I guess that's to be expected.

Since this code is Open Source, anyone who is masochistic enough can fork and modify this code at your leisure. I hope it will remain useful to someone, even if it's just as a basis for something else.

# Linode Dynamic DNS Updater
The Linode Dynamic DNS Updater is a small .NET 2.0 application that allows [Linode](https://www.linode.com) customers to use the company's DNS services as a sort of "dynamic DNS". The Updater uses the [Linode API](https://www.linode.com/products/api/) to update specified domain names with the calling machine's outward-facing IP address. Thus, you can map a domain to a specific machine, even if that machine moves from place to place or is behind a cable or DNS modem that frequently changes IP addresses.

The Linode Dynamic DNS Updater is free software and is released under the [GNU General Public License, Version 2](https://github.com/gpfjeff/linode-dynamic-dns/blob/master/LICENSE). It was written by a satisfied Linode customer for his own personal use, and shared in the hope that other Linode customers might find it useful. It is in no way affiliated with or endorsed by Linode itself.

## Downloadable Binaries

Download version 1.1.1 of the Linode Dynamic DNS Updater:
* [LinodeDynamicDNS.zip](https://github.com/gpfjeff/linode-dynamic-dns/releases/download/v1.1.1/LinodeDynamicDNS.zip) (11.2k)
* [GnuPG Signature](https://github.com/gpfjeff/linode-dynamic-dns/releases/download/v1.1.1/LinodeDynamicDNS.zip.sig) / [Signing Key](https://www.gpf-comics.com/gnupg.php)
* SHA-1: 61d8fd9aa44dd09368339b420ce9b2c21e727e0e
* SHA-256: ff45f316fe6f4c9fa9c95c967117712fd9fe9a2c5981b6e99cd34847090df27e
* SHA-512: cbd4dce8da26b8d74675b9b28e784d4db781a232fd8f3514c344fafc11ef9ddd0ba8f2cfc27ba178129942b7a100edb0c469369b0ee9efb75a5431cd4d293931

Of course, if you prefer not to trust pre-built binaries, that's fine; just pull down the source and build it yourself. The Linode Dynamic DNS Updater was built using Microsoft Visual Studio 2012.

## A Quick and Dirty User Guide

1. If you're not already a Linode customer, [create an account](https://manager.linode.com/session/signup).
2. Log into the [Linode Manager](https://manager.linode.com/) and navigate to the DNS Manager.
3. Select the domain zone you want to work with, or set up a new zone. The Dynamic DNS Updater only works on **master** domain zones, i.e. domains where you've set up Linode to be the primary authoritative source. You cannot use the Updater on a slave zone, because the slave's master will overwrite your updates! (How to buy a domain name and associate it with Linode's DNS is considered out of the scope of this quick guide.)
4. Select the subdomain(s) you wish to use for your dynamic address. You can select as many domains as you like, just so long as you have a valid A (for IPv4) and/or AAAA (for IPv6) record in place. (The initial IP specified for these records is not important because, if everything goes smoothly, it will eventually be overwritten.)
5. Set the TTL ("time to live") record for your dynamic domains to something relatively small; I generally recommend an hour, but you can extend this if you don't anticipate your IP changing as frequently. Whatever you set here should probably match your update frequency, which we'll set below.
6. If you don't already have one, use the My Profile section of the Linode Manager to set up a Linode API key. For extra security, you should probably create a dedicated key for each machine you plan to install the Updater on, just in case the machine becomes compromised and you need to revoke the key. (Then the other keys will continue to function.)
7. If you don't already have it, download and install [Microsoft .NET](https://msdn.microsoft.com/en-us/vstudio/aa496123) if you're planning to use Windows, or [Mono](http://www.mono-project.com/) for other platforms. The Updater is written for .NET 2.0, but Microsoft doesn't offer that version of the runtime anymore; download .NET 3.5 if you're using Microsoft's version.
8. Download (and if necessary, build) the Dynamic DNS Updater. In the end, you'll only need the final EXE. Decide upon a semi-permanent location for it to live, then run it.
  * **Note:** The user account you run this under is significant. The Updater stores its settings in the registry, under HKEY_CURRENT_USER. That means you should run the GUI "setup" mode using the same user account that you plan to run your regular scheduled "batch" job under. You can probably make this easier on yourself by running both modes as a user with admin priviledges. You *can* do both modes as a regular, less priviledged account, but it may complicate a few steps later on.
9. Enter your API key in the API Key text box. Click the Test button to verify that it works.
10. Click the Add button. This brings up a dialog box filled with your top level, master domain zones. Click on a zone to fetch and populate all of the A and AAAA subdomain records for that domain. Find in this list the first domain you want to associate with your dynamic IP, then click Add.
11. Repeat the previous step for each additional domain you want associated with your dynamic IP. **Be careful!** Don't accidentally pick your production website domain or company's master database server! If you pick the wrong domain and screw up the apps running on your Linodes, it's your fault, not mine. :D Also be aware of the difference between IPv4 "A" records and IPv6 "AAAA" records; if your outward-facing IP is IPv4, associating it with an "AAAA" record probably won't work.
12. Once you've selected all your domains, click the Update Now button. If you get a success message, congratulations! Your domains are now pointing to your dynamic IP! (Well, it can take up to 15 minutes for Linode's DNS to reflect the change, but it will get there... eventually.) If you don't get a success message, look for any error messages and try to correct the problem if you can.
13. Your domain(s) now point to your dynamic IP, but they won't stay in sync if your IP changes. You'll need to run the app periodically in "batch" mode to keep your IP up to date. Close the UI by clicking Close (or the X in the corner, or Alt+F4... you know the drill). Then...
  * Under Windows:
    1. Open the Control Panel, choose Administrative Tools, then Task Scheduler. **Note:** If the login you use does not have administrative privileges, you may need to run Task Scheduler as an admin to get this to work.
    2. Set up a task by choosing Create Task. While many of the settings are up to you, make sure you:
      * Set the job to run under the same user account as the one you ran the setup UI under (otherwise it won't see your settings).
      * Choose *Run whether user is logged on or not* to run the job when you're not logged in.
      * Under Triggers, choose *On a Schedule,* then *Repeat Task Every...* Set the frequency to match the TTL you set for your domains above. Then, *For a Duration Of* should be set to *Indefinitely.*
      * Under Actions, in *Program/Script* specify the full path to where you stored the Updater EXE, and specify "-run" under *Add Arguments.*
    3. Note that on some versions of Windows, you may need to add the task's owner to the *[Log on as a batch job](https://technet.microsoft.com/en-us/library/dn221944.aspx)* security policy setting if the user isn't already in one of the approved groups. See [this Stack Overflow article](http://stackoverflow.com/questions/14405433/scheduled-tasks-fail-to-run) for more details.
  * On other platforms:
    1. How you set up a scheduled task varies from platform to platform. Many UNIX-like systems use [cron](https://en.wikipedia.org/wiki/Cron), or a clone of it. Setting up a cron is beyond the scope of this quick guide; however, the linked-to Wikipedia page should give you a decent start. However, make sure that:
      * You specify the full path to the Mono executable, since you're actually calling Mono from the cron and asking it to run the Updater.
      * After Mono, you specify the full path to the Updater EXE.
      * Include the "-run" command-line flag.
      * Schedule the job to run as the same user account that you used to run the setup UI, since that determines where the "registry" file with your settings lives.
      * Set your job to run at a comparable frequency to the TTL you set for your domains above.
14. Make sure to test your scheduled task to make sure it works. If it does, great! If it doesn't... well, walk back through this process and see if you can find what went wrong.
15. If you need to edit your list of domains or update your API key, simply run the EXE again without the "-run" argument to bring back up the GUI. Make your changes and they will be immediately saved. When the scheduled task runs again, it will use the new settings.

## Version History
* v1.0: Initial release
* v1.1: Fixes for Linode API changes
* v1.1.1: Fix for fix to Linode API changes (forgot to update a file)

## Project History

In the past, I used a "professional" dynamic DNS service which I paid a signficant amount for every year. Once I learned about the Linode API and its ability to trivially act in the same capacity, I quickly abandoned the paid service, especially since I could get the same functionality for "free" with the service I was already getting from Linode.

There are a number of scripts and applets for creating a "Linode dynamic DNS" available. Many of these assume Linux scripting capabilities, which is understandable given Linode's Linux-centric services. However, many of the machines that I wanted to provide dynamic DNS capabilites to are Windows machines, and not all of them have Python or Perl installed. It made sense for me to come up with a Windows-centric solution and, if it just so happened to work on other platforms under a compatibility layer like Mono, all the better.

This is actually the second stab I've made at a Linode dynamic DNS updater. The first version was not very configurable, having my API key hard-coded into the source and requiring manual editing of the registry to set up. I wanted to make this more flexible and extensible, so I rewrote it from the ground up.

## Kudos

If you find this app useful, great! I'm glad you like it. I'm not asking for anything in return, but if you suddenly feel the urge to display your gratitude somehow, please swing by my [primary website](https://www.gpf-comics.com/). There's lots of fun things to read over there, and I could use the ad revenue. :) If you're looking for more tangible ways to show appreciation, you can drop some virtual coins in my [tip jar](https://www.gpf-comics.com/tips.php) or, if you *really* like what you see, please [honor me with a subscription to my site](https://www.gpf-comics.com/premium/).
