#!/bin/zsh

qndbremove() {
    curDir="$(pwd)"
    echo $curDir
    cd /d/QN.Expenditure/src/Cex/Cex.Infrastructure
    dotnet ef migrations remove -f
    cd $curDir
}