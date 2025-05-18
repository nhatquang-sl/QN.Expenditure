qndkcp () {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/src/WebAPI/QN.Expenditure.Credentials/qex/
    
    echo -e "\e[32mdocker compose up -d --remove-orphans\e[0m"  # Green color for selected item
    eval "docker compose up -d --remove-orphans && docker image prune -f"

    cd $curDir
}