#!/bin/zsh

qndbinit() {
    curDir="$(pwd)"
    echo $curDir
    cd /d/QN.Expenditure/src/Cex/Cex.Infrastructure
    dotnet ef migrations remove -f
    dotnet ef migrations add InitialCreate
    cd $curDir
}