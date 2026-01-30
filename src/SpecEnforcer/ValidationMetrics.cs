namespace SpecEnforcer;

/// <summary>
/// Contains performance metrics for validation operations.
/// </summary>
public class ValidationMetrics
{
    private long _totalRequestValidations;
    private long _totalResponseValidations;
    private long _totalRequestFailures;
    private long _totalResponseFailures;
    private long _totalRequestValidationTimeMs;
    private long _totalResponseValidationTimeMs;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the total number of request validations performed.
    /// </summary>
    public long TotalRequestValidations => _totalRequestValidations;

    /// <summary>
    /// Gets the total number of response validations performed.
    /// </summary>
    public long TotalResponseValidations => _totalResponseValidations;

    /// <summary>
    /// Gets the total number of request validation failures.
    /// </summary>
    public long TotalRequestFailures => _totalRequestFailures;

    /// <summary>
    /// Gets the total number of response validation failures.
    /// </summary>
    public long TotalResponseFailures => _totalResponseFailures;

    /// <summary>
    /// Gets the average request validation time in milliseconds.
    /// </summary>
    public double AverageRequestValidationTimeMs
    {
        get
        {
            lock (_lock)
            {
                return _totalRequestValidations > 0
                    ? (double)_totalRequestValidationTimeMs / _totalRequestValidations
                    : 0;
            }
        }
    }

    /// <summary>
    /// Gets the average response validation time in milliseconds.
    /// </summary>
    public double AverageResponseValidationTimeMs
    {
        get
        {
            lock (_lock)
            {
                return _totalResponseValidations > 0
                    ? (double)_totalResponseValidationTimeMs / _totalResponseValidations
                    : 0;
            }
        }
    }

    /// <summary>
    /// Records a request validation.
    /// </summary>
    public void RecordRequestValidation(long elapsedMs, bool failed)
    {
        lock (_lock)
        {
            _totalRequestValidations++;
            _totalRequestValidationTimeMs += elapsedMs;
            if (failed)
            {
                _totalRequestFailures++;
            }
        }
    }

    /// <summary>
    /// Records a response validation.
    /// </summary>
    public void RecordResponseValidation(long elapsedMs, bool failed)
    {
        lock (_lock)
        {
            _totalResponseValidations++;
            _totalResponseValidationTimeMs += elapsedMs;
            if (failed)
            {
                _totalResponseFailures++;
            }
        }
    }

    /// <summary>
    /// Resets all metrics to zero.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _totalRequestValidations = 0;
            _totalResponseValidations = 0;
            _totalRequestFailures = 0;
            _totalResponseFailures = 0;
            _totalRequestValidationTimeMs = 0;
            _totalResponseValidationTimeMs = 0;
        }
    }
}
