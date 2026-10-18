namespace AssetManagement.Application.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string message = "Data not found.") : base(message) { }
    }

    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message = "Access denied.") : base(message) { }
    }

    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message) : base(message) { }
    }
}
