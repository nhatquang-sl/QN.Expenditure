```csharp
public enum SpotGridStatus
{
    NEW,
    RUNNING,
    TAKE_PROFIT,
    STOP_LOSS,
    PAUSED
}

public enum SpotGridStepStatus
{
    [Description("Awaiting Buy")] AwaitingBuy, // Bot is waiting for market price to approach entry price

    [Description("Buy Order Placed")] BuyOrderPlaced, // Buy order has been placed successfully

    [Description("Awaiting Sell")] AwaitingSell, // Bot is waiting for market price to approach take-profit price

    [Description("Sell Order Placed")] SellOrderPlaced // Sell order has been placed successfully
}

```

<table>
  <tr>
    <th>Change Fields</th>
    <th>Grid Status</th>
    <th>Actions</th>
  </tr>
  <tr>
    <td rowspan="2">Trigger Price <br>Investment </td>
    <td>NEW</td>
    <td> 
        - Calculate Quote Balance (only for Investment) <br> 
        - Cancel, then Update Initial Step<br /> 
        - Cancel, Delete then Add all Normal Steps <br>
    </td>
  </tr>
  <tr>
    <td>RUNNING</td>
    <td> 
        - Calculate Quote Balance (only for Investment) <br> 
        - Cancel, then Update Initial Step<br /> 
        - Cancel, Delete then Add all Normal Steps <br>
        - <b>NOTE:</b> Change Investment for update Initial Order as a DCA
    </td>
  </tr>
  <tr>
    <td rowspan="2">
        Lower/Upper Price<br>
        Number of Grids
    </td>
    <td>NEW</td>
    <td> 
        - Calculate Quote Balance <br> 
        - Cancel, Delete then Add all Normal Steps <br>
    </td>
  </tr>
  <tr>
    <td>RUNNING</td>
    <td> 
        - Calculate Quote Balance <br> 
        - Cancel, Delete then Add all Normal Steps <br>
    </td>
  </tr>
</table>

