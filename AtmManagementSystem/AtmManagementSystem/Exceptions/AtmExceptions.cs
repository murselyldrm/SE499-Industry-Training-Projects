namespace AtmManagementSystem.Exceptions;

public class AccountNotFoundException(string message) : Exception(message);

public class AccountLockedException(string message) : Exception(message);

public class InvalidPinException(string message) : Exception(message);

public class InsufficientFundsException(string message) : Exception(message);

public class DailyLimitExceededException(string message) : Exception(message);