namespace UaeEInvoice.Data;

public enum ChequeDirection { Outgoing = 1, Incoming = 2 }
public enum ChequeType { Normal = 1, PDC = 2 }

public enum ChequeStatus
{
    Draft = 1,
    Printed = 2,
    HandedOver = 3,    // outgoing
    Deposited = 4,     // incoming
    Presented = 5,
    Cleared = 6,
    Bounced = 7,
    Voided = 8
}
