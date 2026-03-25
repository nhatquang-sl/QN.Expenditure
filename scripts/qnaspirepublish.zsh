qnaspirepublish () {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/QN.Expenditure.AppHost
    
    echo -e "\e[32maspire publish -p docker-compose -o ../credentials/qex\e[0m"  # Green color for selected item
    eval "aspire publish -p docker-compose -o ../credentials/qex"

    cd $curDir
}