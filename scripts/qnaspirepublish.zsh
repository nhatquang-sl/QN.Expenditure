qnaspirepublish () {
    curDir="$(pwd)"
    echo $curDir
    cd $QNEDIR/QN.Expenditure.AppHost
    
    echo -e "\e[32maspire publish -p docker-compose -o ../src/WebAPI/QN.Expenditure.Credentials/qex\e[0m"  # Green color for selected item
    eval "aspire publish -p docker-compose -o ../src/WebAPI/QN.Expenditure.Credentials/qex"

    cd $curDir
}