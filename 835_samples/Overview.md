# EDI File Overview

The below two files are examples of 835 outputs from Centene's two major claims engines.

Amisys is the legacy Centene Claim Adjudicaton Engine, use mostly for Medicaid, but also for some other businesses

Xcelys is the Wellcare Claim Adjudication Engine. Wellcare was aquired by Centene and primarily offers Medicare, but also offers some other businesses.

We are looking to build an 835 parser that is able to ingest 835 files from both Amisys and Xcelys and then convert them to a standard format that can be used and then output 835s in a universal format that can be sent to Zelis, the payment vendor.
