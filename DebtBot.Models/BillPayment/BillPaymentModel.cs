﻿using DebtBot.Models.User;

namespace DebtBot.Models;

public class BillPaymentModel
{
	public UserDisplayModel User { get; set; }
    
	public decimal Amount { get; set; }
}