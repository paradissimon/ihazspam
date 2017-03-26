Source for ihazspam.ca temporary email service
=========================

This is repository contains the https://ihazspam.ca/ complete source code for the .Net Core 1.1 Framework. 
The backend code targets .NET Core and can run on Windows and Linux. 
In the production Linux setup, the web site is fronted by *nginx* which acts as a static file server and reverse proxy for the the REST API.

In the source folder:

*Common*
Common code used across all projects

*MX*: 
Mail Exchanger - a high performance SMTP mail server that accept mail destined to active mailboxes.
This code could be reused and adapted for other purposes. It write raw RFC2822 email content to
an on-disk incoming queue with minimal metadata in a Postgresql database.

*MailExtractor*
Decodes and extracts enqueued email and make them available as static files on the web server.

*MailCleaner*
Delete expired mail content.

*Web*
A minimal web site to allow viewing of received mails and create mailboxes.

=========================

For installation see the *stuff/bootstrap.txt* file.

Deployment build are created with *publish.sh* shell script.