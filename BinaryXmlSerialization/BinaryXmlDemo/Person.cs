using System;

[Serializable]
public class Address
{
    private string _street;
    private string _city;
    private string _state;
    private string _zipCode;

    public string Street
    {
        get { return _street; }
        set { _street = value; }
    }

    public string City
    {
        get { return _city; }
        set { _city = value; }
    }

    public string State
    {
        get { return _state; }
        set { _state = value; }
    }

    public string ZipCode
    {
        get { return _zipCode; }
        set { _zipCode = value; }
    }
}

[Serializable]
public class Person
{
    private string _firstName;
    private string _lastName;
    private int _age;
    private DateTime _birthDate;
    private bool _isEmployed;
    private double _salary;
    private Address _homeAddress;

    private static Random _random = new Random();

    private static T PickRandomItem<T>(params T[] items)
    {
        return items[_random.Next(items.Length)];
    }

    public static Person CreateRandomPerson()
    {
        var dob = new DateTime(_random.Next(1960, 2007), _random.Next(1, 13), _random.Next(1, 29));
        var age = (DateTime.Now - dob).Days / 365;
        return new Person
        {
            FirstName = PickRandomItem("Adam", "Ben", "Charlie", "David", "Eve", "Frank", "Grace", "Hannah"),
            LastName = PickRandomItem("Anderson", "Brown", "Clark", "Davis", "Evans", "Foster", "Garcia", "Harris"),
            Age = age,
            BirthDate = dob,
            IsEmployed = PickRandomItem(true, false),
            Salary = _random.Next(20, 100) * 1000.0,
            HomeAddress = new Address
            {
                Street = $"{_random.Next(1, 12000)} {PickRandomItem("Main", "1st", "Roosevelt", "Washington")} {PickRandomItem("St", "Rd", "Ave", "Place")}",
                City = "Anytown",
                State = "CA",
                ZipCode = $"{_random.Next(10000, 99999)}"
            }
        };
    }

    public string FirstName
    {
        get { return _firstName; }
        set { _firstName = value; }
    }

    public string LastName
    {
        get { return _lastName; }
        set { _lastName = value; }
    }

    public int Age
    {
        get { return _age; }
        set { _age = value; }
    }

    public DateTime BirthDate
    {
        get { return _birthDate; }
        set { _birthDate = value; }
    }

    public bool IsEmployed
    {
        get { return _isEmployed; }
        set { _isEmployed = value; }
    }

    public double Salary
    {
        get { return _salary; }
        set { _salary = value; }
    }

    public Address HomeAddress
    {
        get { return _homeAddress; }
        set { _homeAddress = value; }
    }

    override public string ToString()
    {
        return $"{FirstName} {LastName}, Age: {Age}, BOB: {BirthDate:yyyy, MMM dd}, Employed: {IsEmployed}, Salary: {Salary:C}\nAddress: {HomeAddress.Street}, {HomeAddress.City}, {HomeAddress.State} {HomeAddress.ZipCode}";
    }
}
