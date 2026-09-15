using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace E.DataLinq.Web.Models;

public class DataLinqErrorPageModel
{
    public DataLinqErrorPageModel(Exception exception)
    {
        this.Exception = exception;
        this.Timestamp = DateTime.Now;
        this.QueryString = new NameValueCollection();
    }

    public Exception Exception { get; }

    public DateTime Timestamp { get; }

    public NameValueCollection QueryString { get; set; }

    public string Route { get; set; }

    public string EndpointId { get; set; }

    public string QueryId { get; set; }

    public string ViewId { get; set; }

    public string RequestPath { get; set; }

    public bool IsDevelopment { get; set; }

    public bool ShowStackTrace
        => this.IsDevelopment || this.Exception is NullReferenceException;

    public IEnumerable<string> AllMessages
    {
        get
        {
            var messages = new List<string>();
            var current = this.Exception;

            while (current != null)
            {
                messages.Add(current.Message);
                current = current.InnerException;
            }

            return messages;
        }
    }

    public IEnumerable<Exception> InnerExceptions
    {
        get
        {
            var exceptions = new List<Exception>();
            var current = this.Exception?.InnerException;

            while (current != null)
            {
                exceptions.Add(current);
                current = current.InnerException;
            }

            return exceptions;
        }
    }

    public Exception RootException
    {
        get
        {
            var current = this.Exception;

            while (current?.InnerException != null)
            {
                current = current.InnerException;
            }

            return current;
        }
    }
}
