//skrypt obliczajacy
function ddCalc(showAmort) {
  if (document.getElementById("ddInitialDeposit").value == "") {
    alert("Wprowadź pierwszą wpłatę");
    return;
  }
  if (document.getElementById("ddContribution.value") == "") {
    alert("Wprowadź wartość miesięcznej wpłaty");
    return;
  }
  if (document.getElementById("ddRate.value") == "") {
    alert("Wprowadź oprocentowanie");
    return;
  }
  if (document.getElementById("ddYears.value") == "") {
    alert("Nie wpisales ile lat chcesz oszczedzac.");
    return;
  }

  var mRate = document.getElementById("ddRate").value / 100 / 12;
  var maxMonths = 999999;
  var maxAmount = 99999999999;

  if (document.getElementById("ddYears").value != "")
    maxMonths = document.getElementById("ddYears").value * 12;

  var amort =
    '<table width=100% cellpadding=2 cellspacing=0 border=1 style="text-align:right;"><tr><th>Miesiace</th><th>Wplacone srodki</th><th>Odsetki</th><th>Suma</th></tr>';

  var currentAmount = 0.1 - 0.1;
  var currentContributions = 0.1 - 0.1;
  var currentInterest = 0.1 - 0.1;

  currentAmount += document.getElementById("ddInitialDeposit").value;
  currentContributions += document.getElementById("ddInitialDeposit").value;
  var monthlyAmount = Number(document.getElementById("ddContribution").value);

  var currentMonth = 0;

  while (currentMonth < maxMonths && currentAmount < maxAmount) {
    currentMonth++;
    var interest = Number(currentAmount) * Number(mRate);
    currentInterest = Number(currentInterest) + Number(interest);
    currentContributions = Number(currentContributions) + monthlyAmount;
    currentAmount = Number(currentAmount) + Number(interest) + monthlyAmount;
    amort +=
      "<tr><td>" +
      currentMonth +
      "</td><td>" +
      Number(currentContributions).toFixed(2) +
      "PLN</td><td>" +
      Number(currentInterest).toFixed(2) +
      "PLN</td><td>" +
      Number(currentAmount).toFixed(2) +
      "PLN</td></tr>";
  }

  document.getElementById("ddResult").innerHTML =
    "Dokonując pierwszej wpłaty w kolejności " +
    Number(document.getElementById("ddInitialDeposit").value).toFixed(2) +
    " PLN <br>odkładając miesięcznie " +
    monthlyAmount.toFixed(2) +
    " PLN <br>przy oprocentowaniu rocznym " +
    document.getElementById("ddRate").value +
    "% <br>przez okres " +
    Number(currentMonth) / 12 +
    " lat, <br><br>zgromadzisz " +
    Number(currentAmount).toFixed(2) +
    " PLN<br><br>";

  if (showAmort) {
    document.getElementById("ddResult").innerHTML +=
      '<BR><BR><B>Tabela oszczędności</B><BR><div style="overflow: none;">' +
      amort +
      "</table></div>";
  } else {
    document.getElementById("ddResult").innerHTML +=
      ' <p align="center"><a href="javascript:ddCalc(true);">Pokaż tabelę</a></p>';
  }

  $("#out,#slupki").show("slow");
  $("#s_wklad").text(Number(currentContributions).toFixed(2));
  $("#s_wynik").text(Number(currentAmount).toFixed(2));

  if (currentAmount * 1 > currentContributions * 1) {
    wynik_wysokosc = 120;
    wklad_wysokosc = Math.round(
      120 *
        (1 -
          Math.abs(
            (Number(currentContributions).toFixed(2) -
              Number(currentAmount).toFixed(2)) /
              Number(currentAmount).toFixed(2),
            0
          ))
    );
  } else {
    wklad_wysokosc = 120;
    wynik_wysokosc = Math.round(
      120 *
        (1 -
          Math.abs(
            (Number(currentAmount).toFixed(2) -
              Number(currentContributions).toFixed(2)) /
              Number(currentContributions).toFixed(2),
            0
          ))
    );
    if (wynik_wysokosc <= 0) wynik_wysokosc = 1;
  }

  $("#slupek_wkl").animate(
    {
      height: "" + wklad_wysokosc + "px",
      marginTop: "" + 120 - wklad_wysokosc + "px",
    },
    1500
  );
  $("#slupek_wyn").animate(
    {
      height: "" + wynik_wysokosc + "px",
      marginTop: "" + 120 - wynik_wysokosc + "px",
    },
    1500
  );
}
