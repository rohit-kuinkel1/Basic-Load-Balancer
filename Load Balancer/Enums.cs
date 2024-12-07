namespace Load_Balancer
{
    public enum CircuitState
    {
        Closed,      //Normal operation; requests allowed
        Open,        //Failing; no requests allowed
        HalfOpen     //Testing the waters; limited requests allowed
    }
}
