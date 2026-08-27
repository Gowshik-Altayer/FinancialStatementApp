# FinancialStatementAI — User Guide

FinancialStatementAI helps you turn bank and credit card statements into organized,
searchable transaction records. Upload a statement, and the app reads it, pulls out each
transaction, sorts each one into a spending category, and checks that the numbers add up —
all so you don't have to do it by hand. This guide walks through everything you can do in the
app today, in plain terms, with no technical background required.

## Contents

- [Getting Started](#getting-started)
- [Uploading a Statement](#uploading-a-statement)
- [Understanding Processing Status](#understanding-processing-status)
- [Viewing Your Statements](#viewing-your-statements)
- [Reviewing Transactions](#reviewing-transactions)
- [Managing Categories](#managing-categories)
- [Understanding Reconciliation](#understanding-reconciliation)
- [Reprocessing a Statement](#reprocessing-a-statement)
- [Marking a Statement as Reviewed](#marking-a-statement-as-reviewed)
- [Frequently Asked Questions](#frequently-asked-questions)
- [Getting Help](#getting-help)

## Getting Started

### Creating an account

1. Open the app and click **Register** (or go to the Register page).
2. Enter your first name, last name, and email address.
3. Choose a password of at least 8 characters.
4. Click **Register**. You'll be signed in automatically and taken to your Dashboard.

If the email you entered is already registered, you'll see a message saying an account with
that email already exists — use **Login** instead.

### Logging in

1. Open the app and click **Login** (if you're not already there).
2. Enter your email and password.
3. Click **Login**. You'll land on your Dashboard.

If your email or password doesn't match, you'll see "Invalid email or password" — double-check
both and try again.

### Staying signed in

Your sign-in stays active in your browser for about an hour. If you come back after being away
for a while and your session has expired, the app will automatically send you back to the Login
page — this is normal; just sign in again. You can also sign out deliberately at any time from
the menu in the top navigation bar.

There is currently no "forgot password" option in the app — if you're locked out, contact
whoever manages your FinancialStatementAI account (see [Getting Help](#getting-help)).

## Uploading a Statement

Go to **Statements → Upload** to add a new statement.

**Supported files:**

- File types: **PDF, JPG, JPEG, or PNG**
- Maximum size: **20 MB**
- The file must be a genuine, undamaged document — a password-protected PDF or a corrupted file
  will be rejected with an explanation

**To upload a statement:**

1. Go to the **Statements** section and click **Upload**.
2. Either drag your statement file onto the upload area, or click it to browse for a file on
   your computer.
3. Once selected, you'll see a quick preview and the file's size. If something's wrong with the
   file (wrong type or too large), you'll see a clear error message right away and can pick a
   different file.
4. Click the upload/submit button to send it.
5. You'll be taken straight to the new statement's detail page.

**What happens right after upload:** the statement is saved and appears with a status of
**Uploaded** — but it has not been read or processed yet. This is the one thing that catches
people out: uploading only stores the file. To actually extract the transactions, see the next
section — you'll click **Reprocess** to kick that off.

## Understanding Processing Status

Every statement moves through a series of statuses as it gets processed. You'll see the current
status as a label at the top of the statement's page and as a column in your Statements list.

| Status | What it means | What you should do |
|---|---|---|
| **Uploaded** | The file has been saved, but processing hasn't started yet. | Click **Reprocess** on the statement's page to start processing. |
| **Processing** | The app is currently reading the document. | Wait — this is usually quick. Refresh the page or come back shortly to see the result. |
| **ExtractionFailed** | The app could not read any usable text from the document at all — usually because the scan/photo is too poor quality, the file isn't really a statement, or the PDF has no readable text. | Upload a clearer copy or a higher-quality scan/photo of the statement. Reprocessing the same unreadable file won't help. |
| **ExtractionComplete** | Text was successfully pulled from the document. Transactions are being identified and sorted next. | No action needed — this is a brief in-between state. |
| **ClassificationComplete** | Every transaction has been extracted and sorted into a category. The balances are about to be checked. | No action needed — this is a brief in-between state. |
| **PendingReview** | Processing is finished. Transactions have categories and confidence levels, and the balances have been checked. This is ready for you to look over. | Review the transactions (see [Reviewing Transactions](#reviewing-transactions)) and correct anything that looks wrong, then mark the statement reviewed when you're satisfied. |
| **Verified** | You've reviewed this statement and confirmed it's correct. | Nothing further needed. You can still reprocess it later if you ever need to (for example, after uploading a corrected file), which will move it back through the pipeline. |

**Tip:** the **Reprocess** button is how you start processing a brand-new upload, not only how
you retry a failed one — don't be confused by the label. In most cases, clicking it finishes
within moments and the page updates in place with the final result; exactly how long it takes can
vary depending on how your organization has the app configured.

## Viewing Your Statements

### The Statements list

Go to **Statements** to see every statement you've uploaded, with:

- File name
- Provider (the bank or card issuer, once identified)
- Number of transactions found
- Total debits and total credits
- Processing status
- Reconciliation status (see [Understanding Reconciliation](#understanding-reconciliation))
- Upload date

You can:

- **Search** by typing into the search box — results update automatically as you type.
- **Filter** by processing status and/or reconciliation status using the dropdown filters.
- **Page through** your statements if you have more than fit on one page.

Click any statement's row to open its detail page.

### A statement's detail page

Opening a statement shows you:

- **Statement information** — account holder name, provider, a masked account number,
  the statement period, and currency (once the app has identified them from the document).
- **Balances** — opening balance, closing balance, total debits, and total credits.
- **File & processing** — file type, file size, how many transactions were found, when it was
  uploaded, and when it was last processed.
- **Text extraction** — whether the app was able to read usable text from the file, and how
  (directly from the PDF, or via image/OCR reading).
- **Transactions** — the full list of transactions found on this statement, with their dates,
  descriptions, amounts, categories, and confidence levels. This is the same reviewing experience
  described in the next section.

Fields that haven't been determined yet (because the statement hasn't been processed, or the
document didn't contain that information) are shown as a dash (—).

## Reviewing Transactions

### How categorization works

When a statement is processed, each transaction is automatically read and assigned a spending
category (like Groceries, Utilities, or Travel), along with a **confidence level** — how sure
the app is about that category. You'll see this as a small label on each transaction:

- **High confidence** — the app is confident in this category; it's unlikely to need a change.
- **Review recommended** — reasonably confident, but worth a quick glance.
- **Review required** — low confidence; please check this one and correct it if needed.
- **—** (no label) — the transaction wasn't categorized at all.

### Where to review transactions

There are two places to review:

1. **A statement's own transaction list** — on that statement's detail page, showing every
   transaction on that statement regardless of confidence.
2. **The Review queue** (**Review** in the navigation) — a single list pulling together every
   transaction still awaiting review across *all* of your statements that are in the
   PendingReview status, sorted with the lowest-confidence (most likely to need attention)
   transactions at the top.

There's also an **All Transactions** page (**Transactions** in the navigation) that lets you
search and filter every transaction you own, across every statement, regardless of its
processing status — useful for finding a specific transaction rather than reviewing new ones.

### Correcting a miscategorized transaction

1. Find the transaction in either the statement's transaction list or the Review queue.
2. Click the pencil (edit) icon on that row.
3. Choose the correct category from the dropdown list.
4. Click the checkmark to save (or the "X" to cancel without saving).

Once saved, you'll see a brief confirmation message, and the row now shows a small "verified"
badge next to its category to indicate it's been corrected by a person. The original,
AI-assigned category is never deleted — it's kept in that transaction's history alongside your
correction. Click the clock/history icon on a corrected transaction to see the full trail: what
it was changed from, what it was changed to, who made the change, and when.

### Duplicate transactions

If the app notices a transaction that looks like a duplicate of another one, you'll see a small
icon next to its description flagging it as a potential duplicate. This is informational — it's
there to catch your eye during review; there's no separate action required.

## Managing Categories

Today, you can **choose from** the existing list of categories when correcting a transaction —
categories like Food & Dining, Groceries, Transportation, Fuel, Travel, Shopping, Software &
SaaS, Utilities, Insurance, Healthcare, Payroll, Rent, Loan Payment, Bank Fee, Interest, Tax,
Transfer, Refund, Income, and Other are available out of the box.

There is **no screen yet** to create new categories, rename existing ones, or deactivate ones you
don't use — that capability is planned for a future update. If you need a category that doesn't
exist, choose the closest match (often "Other") for now.

## Understanding Reconciliation

After a statement's transactions are extracted and categorized, the app checks whether the
numbers actually add up — using simple, exact arithmetic (never AI), so this check is always
trustworthy:

> Opening balance + money in (credits) − money out (debits) should equal the statement's own
> reported closing balance.

You'll see the result as a **reconciliation status** chip on the Statements list and on a
statement's detail page:

| Status | What it means |
|---|---|
| **Reconciled** | The math checks out — the calculated closing balance matches the statement's reported closing balance. |
| **Mismatch** | The numbers don't add up. The app shows you the actual opening balance, total credits, total debits, the closing balance it calculated, the closing balance the statement reported, and exactly how large the gap is — so you can see where to investigate. |
| **Insufficient information** | The app couldn't find an opening balance and/or closing balance on the statement, so it isn't able to run this check at all. |

A dedicated cross-statement reconciliation report isn't available yet — for now, check
reconciliation status per statement from the Statements list (which you can filter by
reconciliation status) or from an individual statement's page.

## Reprocessing a Statement

Use **Reprocess** when:

- You've just uploaded a statement and need to start processing it for the first time.
- A statement's extraction failed and you've since uploaded a clearer copy.
- You simply want the pipeline to run again (for example, after a general improvement to how the
  app reads statements).

**To reprocess:**

1. Open the statement's detail page.
2. Click **Reprocess**.
3. Wait for the button's spinner to finish — the page then updates in place with the refreshed
   status, extracted fields, and transaction list.

If reprocessing fails, you'll see a brief notification saying so; the statement keeps its
previous data rather than losing anything.

## Marking a Statement as Reviewed

Once a statement reaches the **PendingReview** status, a **Mark reviewed** button appears on its
detail page. Click it once you're satisfied with its categorized transactions and reconciliation
result — this moves the statement to **Verified**.

This button is only available while a statement is in PendingReview. If you reprocess a
statement that's already Verified, it will run through the pipeline again and end up back in
PendingReview, ready to be reviewed and verified again.

## Frequently Asked Questions

**Why did my statement fail to process (ExtractionFailed)?**
The app couldn't find any usable, readable text in the file — this is most common with a poor
quality scan or photo, a file that isn't actually a bank/card statement, or a PDF with no real
text layer. Try uploading a clearer, higher-resolution copy. Reprocessing the exact same
unreadable file won't produce a different result.

**Why is my statement still showing "Uploaded" and has no transactions?**
Uploading a file only saves it — it doesn't start processing automatically. Open the statement
and click **Reprocess** to begin extraction.

**What if a transaction is miscategorized?**
Correct it yourself: click the pencil icon on that transaction and choose the right category.
Your correction is saved immediately, is fully recorded in that transaction's history (including
what it changed from, who changed it, and when), and the original category is never lost.

**Can I add my own custom categories?**
Not yet. You can only choose from the existing built-in category list when correcting a
transaction. Category management (creating, renaming, deactivating categories) is planned for a
future update.

**Is my data secure?**
Your account is protected by a sign-in token that expires automatically after about an hour of
inactivity, requiring you to sign back in. Every statement and transaction you upload is tied to
your account only — other users cannot see or access your data. Uploaded files are checked for
genuine, valid content before being accepted (not just their file name), and corrupted or
password-protected files are rejected outright rather than processed.

**What file types and sizes can I upload?**
PDF, JPG, JPEG, or PNG files up to 20 MB each.

**Why was I suddenly signed out?**
Most commonly, your sign-in session expired (sessions last about an hour). It can also happen if
there's a temporary problem reaching the server. Either way, simply sign in again to continue.

**Why can't I see the "Reconciliation" or "Categories" pages do anything?**
Reconciliation results are shown per statement (on the Statements list and each statement's
detail page) rather than as a separate report page today. Category management is a screen that
hasn't been built yet. Both of these navigation items currently explain this in the app itself.

## Getting Help

This guide covers everything currently available in the app. If you run into a problem it
doesn't answer — an account issue, a statement that consistently won't process, or anything
else — please contact your system administrator or whoever manages your organization's
FinancialStatementAI installation.
