using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Magnetizing_FPG.Core.Domain
{
    /// <summary>
    /// Represents the result of a validation operation with errors and warnings.
    /// </summary>
    public class ValidationResult
    {
        private readonly List<ValidationMessage> _messages = new List<ValidationMessage>();

        /// <summary>
        /// Gets all validation messages.
        /// </summary>
        public IReadOnlyList<ValidationMessage> Messages => _messages.AsReadOnly();

        /// <summary>
        /// Gets all error messages.
        /// </summary>
        public IEnumerable<ValidationMessage> Errors => _messages.Where(m => m.Level == ValidationLevel.Error);

        /// <summary>
        /// Gets all warning messages.
        /// </summary>
        public IEnumerable<ValidationMessage> Warnings => _messages.Where(m => m.Level == ValidationLevel.Warning);

        /// <summary>
        /// Gets all informational messages.
        /// </summary>
        public IEnumerable<ValidationMessage> Info => _messages.Where(m => m.Level == ValidationLevel.Info);

        /// <summary>
        /// Indicates whether the validation passed (no errors).
        /// </summary>
        public bool IsValid => !Errors.Any();

        /// <summary>
        /// Indicates whether there are any validation messages.
        /// </summary>
        public bool HasMessages => _messages.Any();

        /// <summary>
        /// Gets the count of errors.
        /// </summary>
        public int ErrorCount => Errors.Count();

        /// <summary>
        /// Gets the count of warnings.
        /// </summary>
        public int WarningCount => Warnings.Count();

        /// <summary>
        /// Gets the most severe validation level present.
        /// </summary>
        public ValidationLevel HighestLevel => 
            _messages.Any() ? _messages.Max(m => m.Level) : ValidationLevel.Info;

        /// <summary>
        /// Adds an error message.
        /// </summary>
        public ValidationResult AddError(string message, string property = null, string context = null)
        {
            _messages.Add(new ValidationMessage(ValidationLevel.Error, message, property, context));
            return this;
        }

        /// <summary>
        /// Adds a warning message.
        /// </summary>
        public ValidationResult AddWarning(string message, string property = null, string context = null)
        {
            _messages.Add(new ValidationMessage(ValidationLevel.Warning, message, property, context));
            return this;
        }

        /// <summary>
        /// Adds an informational message.
        /// </summary>
        public ValidationResult AddInfo(string message, string property = null, string context = null)
        {
            _messages.Add(new ValidationMessage(ValidationLevel.Info, message, property, context));
            return this;
        }

        /// <summary>
        /// Adds a validation message with the specified level.
        /// </summary>
        public ValidationResult AddMessage(ValidationLevel level, string message, string property = null, string context = null)
        {
            _messages.Add(new ValidationMessage(level, message, property, context));
            return this;
        }

        /// <summary>
        /// Merges another validation result into this one.
        /// </summary>
        public ValidationResult Merge(ValidationResult other, string contextPrefix = null)
        {
            if (other == null) return this;

            foreach (var message in other._messages)
            {
                var context = string.IsNullOrEmpty(contextPrefix) 
                    ? message.Context 
                    : string.IsNullOrEmpty(message.Context) 
                        ? contextPrefix 
                        : $"{contextPrefix}.{message.Context}";

                _messages.Add(new ValidationMessage(message.Level, message.Message, message.Property, context));
            }

            return this;
        }

        /// <summary>
        /// Clears all validation messages.
        /// </summary>
        public ValidationResult Clear()
        {
            _messages.Clear();
            return this;
        }

        /// <summary>
        /// Gets a formatted summary of the validation result.
        /// </summary>
        public string GetSummary()
        {
            if (!HasMessages) return "Validation passed with no issues.";

            var summary = new StringBuilder();
            
            if (ErrorCount > 0)
                summary.AppendLine($"Errors: {ErrorCount}");
            
            if (WarningCount > 0)
                summary.AppendLine($"Warnings: {WarningCount}");

            return summary.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets a detailed formatted report of all messages.
        /// </summary>
        public string GetDetailedReport()
        {
            if (!HasMessages) return "Validation passed with no issues.";

            var report = new StringBuilder();
            report.AppendLine($"Validation Result: {GetSummary()}");
            report.AppendLine();

            var groupedMessages = _messages.GroupBy(m => m.Level).OrderByDescending(g => g.Key);

            foreach (var group in groupedMessages)
            {
                report.AppendLine($"{group.Key}s:");
                foreach (var message in group)
                {
                    var prefix = "  ";
                    if (!string.IsNullOrEmpty(message.Context))
                        prefix += $"[{message.Context}] ";
                    if (!string.IsNullOrEmpty(message.Property))
                        prefix += $"{message.Property}: ";

                    report.AppendLine($"{prefix}{message.Message}");
                }
                report.AppendLine();
            }

            return report.ToString().TrimEnd();
        }

        /// <summary>
        /// Throws a ValidationException if there are any errors.
        /// </summary>
        public ValidationResult ThrowIfInvalid()
        {
            if (!IsValid)
            {
                throw new ValidationException($"Validation failed with {ErrorCount} error(s)", this);
            }
            return this;
        }

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        public static ValidationResult Success()
        {
            return new ValidationResult();
        }

        /// <summary>
        /// Creates a validation result with a single error.
        /// </summary>
        public static ValidationResult Error(string message, string property = null, string context = null)
        {
            return new ValidationResult().AddError(message, property, context);
        }

        /// <summary>
        /// Creates a validation result with a single warning.
        /// </summary>
        public static ValidationResult Warning(string message, string property = null, string context = null)
        {
            return new ValidationResult().AddWarning(message, property, context);
        }

        public override string ToString()
        {
            return GetSummary();
        }
    }

    /// <summary>
    /// Represents a single validation message.
    /// </summary>
    public class ValidationMessage
    {
        public ValidationLevel Level { get; }
        public string Message { get; }
        public string Property { get; }
        public string Context { get; }
        public DateTime Timestamp { get; }

        public ValidationMessage(ValidationLevel level, string message, string property = null, string context = null)
        {
            Level = level;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Property = property;
            Context = context;
            Timestamp = DateTime.UtcNow;
        }

        public override string ToString()
        {
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(Context))
                parts.Add($"[{Context}]");
            
            if (!string.IsNullOrEmpty(Property))
                parts.Add($"{Property}:");
            
            parts.Add(Message);

            return $"{Level}: {string.Join(" ", parts)}";
        }
    }

    /// <summary>
    /// Validation message severity levels.
    /// </summary>
    public enum ValidationLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// Exception thrown when validation fails.
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationResult ValidationResult { get; }

        public ValidationException(string message, ValidationResult validationResult) 
            : base(message)
        {
            ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        }

        public ValidationException(string message, ValidationResult validationResult, Exception innerException) 
            : base(message, innerException)
        {
            ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
        }

        public override string ToString()
        {
            var baseString = base.ToString();
            var detailedReport = ValidationResult.GetDetailedReport();
            
            return $"{baseString}\n\nValidation Details:\n{detailedReport}";
        }
    }
}