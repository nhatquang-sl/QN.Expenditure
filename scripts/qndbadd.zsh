#!/bin/zsh

qndbadd() {
    local ver=$1
    if [[ ${#ver} -gt 0 ]]; then # Up
      curDir="$(pwd)"
      echo $curDir
      cd $QNEDIR/src/Cex/Cex.Infrastructure
      dotnet ef migrations add $ver
      cd $curDir
    fi
}