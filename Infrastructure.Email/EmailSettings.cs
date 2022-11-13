﻿namespace Infrastructure.Email;

public class EmailSettings
{
    public string UserName { get; set; }
    public string Password { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public MailAddressSettings MailAddress { get; set; }
}

public class MailAddressSettings
{
    public string Address { get; set; }
    public string? DisplayName { get; set; }
}