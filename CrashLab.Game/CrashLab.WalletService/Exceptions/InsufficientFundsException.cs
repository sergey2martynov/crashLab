namespace CrashLab.WalletService.Exceptions;

public class InsufficientFundsException(string? message) : Exception(message);