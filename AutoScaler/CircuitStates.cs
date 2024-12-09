namespace LoadBalancer
{
    public enum CircuitStates
    {
        Closed,      //Normal operation; requests allowed
        Open,        //Failing; no requests allowed
        HalfOpen     //Testing the waters; limited requests allowed
    }
}
