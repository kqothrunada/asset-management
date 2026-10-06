using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Globalization;

namespace AssetManagement.Infrastructure
{
    public sealed class EncryptedDecimalConverter : ValueConverter<decimal, string>
    {
        public EncryptedDecimalConverter(IDataProtector protector) : base(
            v => protector.Protect(v.ToString(CultureInfo.InvariantCulture)),
            s => decimal.Parse(protector.Unprotect(s), CultureInfo.InvariantCulture))
        {
        }
    }
}
