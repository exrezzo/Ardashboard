module F.Core.Domain

open System

type HtmlBankMsg = { Id: string; HtmlBody: string }
type BankTransaction =
        { Amount: double
          Place: string
          Occurred: DateTime }